const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '..');
const toolRoot = process.env.VAIRAS_CONTRACT_TOOL_ROOT || path.join(root, 'scripts/contract-tools');
const tool = createRequire(path.join(path.resolve(toolRoot), 'package.json'));
const YAML = tool('yaml');
const { Parser } = tool('@asyncapi/parser');
const Ajv = tool('ajv/dist/2020').default;
const formats = tool('ajv-formats');
const { Parser: DbmlParser } = tool('@dbml/core');
const read = file => fs.readFileSync(path.join(root, file), 'utf8').replace(/^\uFEFF/, '');
function check(value, message) { if (!value) throw Error(message); }

(async () => {
  const source = read('api/learning-tracking.asyncapi.yaml');
  const parsed = await new Parser().parse(source);
  const errors = parsed.diagnostics.filter(d => d.severity === 0);
  check(parsed.document && errors.length === 0, JSON.stringify(errors));
  const async = YAML.parse(source);
  const ajv = new Ajv({ strict: false, allErrors: true }); formats(ajv);
  let eventCount = 0;
  for (const file of ['evidence/learning-event-samples.json', 'evidence/school-event-samples.json']) {
    if (!fs.existsSync(path.join(root, file)) && file.includes('school-') && !fs.existsSync(path.join(root, '../Vairas.SchoolManagement'))) continue;
    check(fs.existsSync(path.join(root, file)), `Run the unit and five-service suites first: ${file}`);
    const captured = JSON.parse(read(file));
    for (const event of captured.events) {
      for (const [schema, value] of [[async.components.schemas.IntegrationHeaders, event.headers], [async.components.schemas[`${event.eventType}Payload`], event.payload]]) {
        check(schema, `Missing ${event.eventType} schema`);
        const valid = ajv.compile(schema);
        check(valid(value), `${event.eventType}: ${JSON.stringify(valid.errors)}`);
      }
      eventCount++;
    }
  }
  const openapi = JSON.parse(read('api/learning-tracking.openapi.json'));
  ajv.addSchema({ ...openapi, $id: 'urn:vairas:learning-openapi' });
  const examples = read('Vairas.LearningTracking/Vairas.LearningTracking.http');
  const variables = Object.fromEntries([...examples.matchAll(/^@(\w+)\s*=\s*(.*)$/gm)].map(m => [m[1], m[2].trim()]));
  const replace = s => s.replace(/\{\{(\w+)\}\}/g, (_, key) => variables[key] ?? key);
  const bindRefs = x => Array.isArray(x) ? x.map(bindRefs) : x && typeof x === 'object' ? Object.fromEntries(Object.entries(x).map(([k,v]) => [k, k === '$ref' && v.startsWith('#/') ? 'urn:vairas:learning-openapi' + v : bindRefs(v)])) : x;
  let requestCount = 0, invalidCount = 0;
  const covered = new Set();
  for (const block of examples.split(/^###/m)) {
    const request = block.match(/^(GET|POST|PUT|PATCH|DELETE)\s+(\S+)/m);
    if (!request) continue;
    const method = request[1].toLowerCase(), url = new URL(replace(request[2]));
    if (url.pathname.startsWith('/health')) continue;
    const template = Object.keys(openapi.paths).find(p => new RegExp('^' + p.replace(/[.*+?^$()|[\]\\]/g, '\\$&').replace(/\{[^}]+\}/g, '[^/]+') + '$').test(url.pathname) && openapi.paths[p][method]);
    check(template, `Undocumented example: ${method} ${url.pathname}`);
    covered.add(`${method} ${template}`);
    const operation = openapi.paths[template][method];
    const body = block.match(/\r?\n\r?\n\s*(\{[\s\S]*\})\s*$/);
    if (!body) continue;
    const schema = operation.requestBody?.content?.['application/json']?.schema;
    check(schema, `Example body has no schema: ${template}`);
    const validate = ajv.compile(bindRefs(schema));
    const valid = validate(JSON.parse(replace(body[1])));
    const expectedInvalid = /expected 400/i.test(block);
    check(expectedInvalid ? !valid : valid, `${method} ${template}: ${JSON.stringify(validate.errors)}`);
    expectedInvalid ? invalidCount++ : requestCount++;
  }
  const operations = Object.entries(openapi.paths).flatMap(([p,v]) => Object.keys(v).filter(m => ['get','post','put','patch','delete'].includes(m)).map(m => `${m} ${p}`));
  check(operations.every(o => covered.has(o)), 'Missing HTTP examples: ' + operations.filter(o => !covered.has(o)).join(', '));
  const parser = new DbmlParser();
  const database = parser.parse(read('database/implementation.dbml'), 'dbmlv2');
  const tables = database.schemas.reduce((n,s) => n + s.tables.length, 0);
  check(tables === 38, `Expected 38 implemented tables, got ${tables}`);
  const result = { asyncapiErrors: errors.length, asyncapiWarnings: parsed.diagnostics.filter(d => d.severity !== 0).length,
    capturedEventsValidated: eventCount, positiveRequestBodiesValidated: requestCount, expectedInvalidBodies: invalidCount,
    httpOperationsCovered: covered.size, implementedOperations: operations.length, dbmlTables: tables };
  fs.writeFileSync(path.join(root, 'evidence/contracts-validation.json'), JSON.stringify(result, null, 2));
  console.log(JSON.stringify(result));
})().catch(e => { console.error(e.message || JSON.stringify(e, null, 2)); process.exitCode = 1; });
