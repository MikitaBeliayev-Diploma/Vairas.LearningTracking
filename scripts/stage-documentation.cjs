const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const YAML = require('./contract-tools/node_modules/yaml');
const root = path.resolve(__dirname, '..');
const target = path.join(root, 'documentation-update/tree');
const mappings = {
  'api/learning-tracking.openapi.yaml': 'Communication/Api/learning-tracking.openapi.yaml',
  'api/learning-tracking.asyncapi.yaml': 'Communication/Async Api/learning-tracking.asyncapi.yaml',
  'database/implementation.dbml': 'Implementation/Learning Tracking/database/implementation.dbml',
  'CONTRACT-NOTES.md': 'Implementation/Learning Tracking/CONTRACT-NOTES.md',
  'VALIDATION.md': 'Implementation/Learning Tracking/VALIDATION.md',
  'docs/policies.md': 'Implementation/Learning Tracking/policies.md',
  'evidence/contracts-validation.json': 'Implementation/Learning Tracking/contracts-validation.json'
};
function save(destination, data) {
  const file = path.join(target, destination);
  fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, data);
}
for (const [from, to] of Object.entries(mappings)) save(to, fs.readFileSync(path.join(root, from)));
for (const name of fs.readdirSync(path.join(root, 'Vairas.LearningTracking.Persistance/Migrations/Sql'))) {
  save('Implementation/Learning Tracking/database/migration-source/' + name,
    fs.readFileSync(path.join(root, 'Vairas.LearningTracking.Persistance/Migrations/Sql', name)));
}
for (const name of ['bootstrap-roles.sql','bootstrap-delivery.sql','prepare-schema.sql','canonical-roles.sql','reliability-grants.sql']) {
  save('Implementation/Learning Tracking/database/' + name, fs.readFileSync(path.join(root, 'database', name)));
}
// Keep provider additions separate: replace provider OpenAPI only after owner review.
for (const [from, to] of [
  ['../Vairas.SchoolManagement/.cache/school-management.openapi.json', 'school-management'],
  ['../Vairas.Scheduling/api/scheduling.openapi.json', 'scheduling']
]) {
  const file = path.resolve(root, from);
  if (fs.existsSync(file)) save('Implementation/Learning Tracking/provider-api/' + to + '.openapi.yaml', YAML.stringify(JSON.parse(fs.readFileSync(file, 'utf8'))));
}
function files(folder) { return fs.readdirSync(folder, {withFileTypes:true}).flatMap(x => x.isDirectory() ? files(path.join(folder,x.name)) : [path.join(folder,x.name)]); }
const manifest = files(target).map(file => ({ path: path.relative(target,file).replaceAll('\\','/'), sha256: crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex') }));
fs.writeFileSync(path.join(root, 'documentation-update/manifest.json'), JSON.stringify({status:'STAGED_NOT_APPLIED',files:manifest},null,2));
console.log(`Staged ${manifest.length} files under documentation-update/tree. Documentation repository was not modified.`);
