extern alias DocumentsApi;
extern alias SchoolTests;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NUnit.Framework;
using Doc = Vairas.DocumentManagement.Business;
using DocPersistence = Vairas.DocumentManagement.Persistance;
using Vairas.DocumentManagement.Integrations;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

public sealed partial class FiveServiceTests
{
    static partial void ConfigureDocumentAuth(Dictionary<string,string?> configuration)
    {
        configuration["Authentication:UserAudiences:5"]="vairas-document-management";
        configuration["Authentication:WorkloadClients:document-management:SecretHash"]=configuration["Authentication:WorkloadClients:school-management:SecretHash"];
        SetScopes(configuration,"document-management",["identity.validate","profiles.status.read","profiles.binding.read","profiles.documents.read","school.documents.read","learning.documents.read","learning.documents.decide"]);
    }
    [Test,Order(4)]
    public async Task Six_real_HTTP_services_generate_publish_download_and_invalidate_exact_completion_artifacts()
    {
        var suffix=Guid.NewGuid().ToString("N"); var database="documents_"+suffix;
        var roles=new Dictionary<string,string> { ["v_document_management_migration"]="dm_owner_"+suffix,["v_document_management_runtime"]="dm_runtime_"+suffix,["v_document_management_delivery"]="dm_delivery_"+suffix };
        try
        {
            foreach(var role in roles.Values) await PostgresFixture.Sql(pg.AdminConnection,$"CREATE ROLE {role} LOGIN PASSWORD '{env.Secret}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
            await PostgresFixture.Sql(pg.AdminConnection,$"CREATE DATABASE {database} OWNER {roles["v_document_management_migration"]}");
            string Connection(string role)=>new NpgsqlConnectionStringBuilder(pg.AdminConnection){Database=database,Username=roles[role],Password=env.Secret}.ConnectionString;
            await using(var db=new DocPersistence.DocumentDbContext(new DbContextOptionsBuilder<DocPersistence.DocumentDbContext>().UseNpgsql(Connection("v_document_management_migration")).Options))await db.Database.MigrateAsync();
            var grants=DocPersistence.Migrations.Sql.Read("runtime-grants.sql"); foreach(var role in roles)grants=grants.Replace(role.Key,role.Value,StringComparison.Ordinal);
            await PostgresFixture.Sql(Connection("v_document_management_migration"),grants);
            var config=new Dictionary<string,string?> { ["ConnectionStrings:DocumentManagement"]=Connection("v_document_management_runtime"),["Documents:WorkersEnabled"]="false",["Dependencies:ClientSecret"]=env.Secret };
            await using var factory=new ServiceFactory<DocumentsApi::Program>(Path.GetFullPath(Path.Combine(PostgresFixture.Root,"../Vairas.DocumentManagement/Vairas.DocumentManagement")),config,env,services=>
            { services.AddSingleton<Doc.IPrivateFiles,DocumentFilesFixture>(); services.AddSingleton<Doc.IFileScanner,DocumentScannerFixture>(); services.AddSingleton<Doc.IDocumentRenderer,DocumentRendererFixture>(); });
            using var client=factory.CreateClient(new(){BaseAddress=new("https://localhost:7700")});
            using(var scope=factory.Services.CreateScope())
            {
                var settings=scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Vairas.DocumentManagement.Configuration.Options.DependencyOptions>>().Value;
                Assert.That(settings.ClientId,Is.EqualTo("document-management"));
                using(var tokenResponse=await env.Auth.PostAsync("/connect/token",new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"]="client_credentials",["client_id"]=settings.ClientId,["client_secret"]=settings.ClientSecret,["scope"]=settings.Scope })))
                    Assert.That(tokenResponse.IsSuccessStatusCode,Is.True,tokenResponse.IsSuccessStatusCode ? "Workload admitted." : await tokenResponse.Content.ReadAsStringAsync());
                try { await scope.ServiceProvider.GetRequiredService<Vairas.DocumentManagement.Integrations.IWorkloadTokens>().GetAsync(default); }
                catch(Doc.BusinessException e) { Assert.Fail("Document workload bootstrap: "+e.Code); }
            }
            using(var anonymous=await client.GetAsync($"/api/v1/enrollments/{enrollment}/documents?schoolId={school}"))Assert.That(anonymous.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
            var template=await Send(client,HttpMethod.Post,$"/api/v1/schools/{school}/document-templates",env.Administrator,new Doc.CreateTemplateRequest("CompletionReport","DEMO HTTP report"),true);
            var templateId=template.GetProperty("templateId").GetGuid();
            using var upload=new HttpRequestMessage(HttpMethod.Post,$"/api/v1/schools/{school}/document-files?purpose=Template"); upload.Headers.Authorization=new("Bearer",env.Administrator.Tokens.AccessToken);upload.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));
            var multipart=new MultipartFormDataContent();multipart.Add(new ByteArrayContent(DocxRenderer.DemoTemplate("CompletionReport")),"file","demo.docx");upload.Content=multipart;
            using var uploadResponse=await client.SendAsync(upload); var fileText=await uploadResponse.Content.ReadAsStringAsync();Assert.That(uploadResponse.StatusCode,Is.EqualTo(HttpStatusCode.Created),fileText);
            var fileId=JsonDocument.Parse(fileText).RootElement.GetProperty("fileId").GetGuid();
            template=await Send(client,HttpMethod.Post,$"/api/v1/schools/{school}/document-templates/{templateId}/versions",env.Administrator,new{fileId,expectedRevision=template.GetProperty("revision").GetInt64()},true);
            var templateVersion=template.GetProperty("versions")[0].GetProperty("templateVersionId").GetGuid();
            await Send(client,HttpMethod.Post,$"/api/v1/schools/{school}/document-templates/{templateId}/versions/{templateVersion}/activate",env.Administrator,new Doc.RevisionRequest(template.GetProperty("revision").GetInt64(),"Explicit demo activation."),true);
            var evidence=await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/evidence-snapshots?schoolId={school}",env.Administrator,new PrepareEvidenceRequest(EvidenceKind.CompletionReport),true);
            Assert.That(evidence.GetProperty("status").GetString(),Is.EqualTo("Ready"));
            var sourceVersion=$"evidence-{evidence.GetProperty("snapshotId").GetGuid():D}-{evidence.GetProperty("dependencyRevision").GetInt64()}";
            var job=await Send(client,HttpMethod.Post,$"/api/v1/enrollments/{enrollment}/completion-reports?schoolId={school}",env.Administrator,new Doc.GenerateTrainingDocumentRequest(sourceVersion,templateId),true);
            using(var scope=factory.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<Doc.DocumentService>().ProcessJobAsync(school,job.GetProperty("jobId").GetGuid(),default);
            job=await Send(client,HttpMethod.Get,$"/api/v1/document-jobs/{job.GetProperty("jobId").GetGuid()}?schoolId={school}",env.Administrator);Assert.That(job.GetProperty("status").GetString(),Is.EqualTo("Completed"),job.ToString());
            var documentId=job.GetProperty("documentId").GetGuid();var document=await Send(client,HttpMethod.Get,$"/api/v1/documents/{documentId}?schoolId={school}",env.Administrator);var versionId=document.GetProperty("versions")[0].GetProperty("documentVersionId").GetGuid();
            document=await Send(client,HttpMethod.Post,$"/api/v1/documents/{documentId}/versions/{versionId}/review?schoolId={school}",env.Administrator,new Doc.VersionCommandRequest(document.GetProperty("revision").GetInt64(),1,"Human fixture review."),true);
            var operation=Guid.NewGuid();using(var response=await SchoolTests::Vairas.SchoolManagement.Tests.IntegrationEnvironment.Send(client,HttpMethod.Post,$"/api/v1/documents/{documentId}/versions/{versionId}/publish?schoolId={school}",env.Administrator.Tokens.AccessToken,new Doc.PublishRequest(document.GetProperty("revision").GetInt64(),1,"Ready","Publish checked fixture."),operation))Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.Accepted),await response.Content.ReadAsStringAsync());
            using(var scope=factory.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<Doc.DocumentService>().ProcessPublicationAsync(school,operation,default);
            document=await Send(client,HttpMethod.Get,$"/api/v1/documents/{documentId}?schoolId={school}",env.Administrator);Assert.That(document.GetProperty("versions")[0].GetProperty("state").GetString(),Is.EqualTo("PublishedReady"));
            var download=await Send(client,HttpMethod.Get,$"/api/v1/documents/{documentId}/download?schoolId={school}",env.Outsider);
            using(var response=await SchoolTests::Vairas.SchoolManagement.Tests.IntegrationEnvironment.Send(client,HttpMethod.Get,download.GetProperty("downloadUrl").GetString()!,env.Outsider.Tokens.AccessToken))Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK),await response.Content.ReadAsStringAsync());
            // A real Learning dependency invalidation is persisted; the delayed-event download must already fail via the owner check.
            await using(var db=pg.OwnerDb()) { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE student_training SET dependency_revision=dependency_revision+1 WHERE school_id={school} AND id={enrollment}"); }
            using(var denied=await SchoolTests::Vairas.SchoolManagement.Tests.IntegrationEnvironment.Send(client,HttpMethod.Get,download.GetProperty("downloadUrl").GetString()!,env.Outsider.Tokens.AccessToken))Assert.That(denied.StatusCode,Is.EqualTo(HttpStatusCode.Conflict));
            using(var scope=factory.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<Doc.DocumentService>().ReconcileAsync(school,enrollment,default);
            document=await Send(client,HttpMethod.Get,$"/api/v1/documents/{documentId}?schoolId={school}",env.Administrator);Assert.That(document.GetProperty("currentVersionId").ValueKind,Is.EqualTo(JsonValueKind.Null));
            using var openapi=await client.GetAsync("/openapi/v1.json");var json=await openapi.Content.ReadAsStringAsync();Assert.That(openapi.StatusCode,Is.EqualTo(HttpStatusCode.OK),json);
            var parsed=Microsoft.OpenApi.OpenApiDocument.Parse(json,"json");Assert.That(parsed.Diagnostic!.Errors,Is.Empty);
            var apiDirectory=Path.GetFullPath(Path.Combine(PostgresFixture.Root,"../Vairas.DocumentManagement/api"));Directory.CreateDirectory(apiDirectory);await File.WriteAllTextAsync(Path.Combine(apiDirectory,"document-management.openapi.json"),json);
            using var ready=await client.GetAsync("/health/ready");Assert.That(ready.StatusCode,Is.EqualTo(HttpStatusCode.OK),await ready.Content.ReadAsStringAsync());
            foreach(var owner in new[]{(Client:env.Client,Service:"Vairas.SchoolManagement",File:"school-management.openapi.json"),(Client:env.Users,Service:"Vairas.UserManagement",File:"user-management.openapi.json"),(Client:env.Auth,Service:"Vairas.Authentication",File:"authentication.openapi.json")})
            {
                using var response=await owner.Client.GetAsync("/openapi/v1.json");var contract=await response.Content.ReadAsStringAsync();Assert.That(response.IsSuccessStatusCode,Is.True,contract);
                var ownerDocument=Microsoft.OpenApi.OpenApiDocument.Parse(contract,"json");Assert.That(ownerDocument.Diagnostic!.Errors,Is.Empty);
                var directory=Path.GetFullPath(Path.Combine(PostgresFixture.Root,"..",owner.Service,"api"));Directory.CreateDirectory(directory);await File.WriteAllTextAsync(Path.Combine(directory,owner.File),contract);
            }
        }
        finally { NpgsqlConnection.ClearAllPools();await PostgresFixture.Sql(pg.AdminConnection,$"DROP DATABASE IF EXISTS {database} WITH(FORCE)");foreach(var role in roles.Values)await PostgresFixture.Sql(pg.AdminConnection,$"DROP ROLE IF EXISTS {role}"); }
    }
    private sealed class DocumentFilesFixture : Doc.IPrivateFiles
    {
        private readonly Dictionary<string,byte[]> files=[];
        public Task PutAsync(string key,byte[] bytes,string mediaType,CancellationToken ct){files.Add(key,bytes);return Task.CompletedTask;}
        public Task<byte[]> GetAsync(string key,long expectedSize,CancellationToken ct)=>Task.FromResult(files[key]);
    }
    private sealed class DocumentScannerFixture : Doc.IFileScanner { public Task<string> ScanAsync(byte[] bytes,CancellationToken ct)=>Task.FromResult("Clean"); }
    private sealed class DocumentRendererFixture : Doc.IDocumentRenderer
    {
        private readonly DocxRenderer renderer=new(new(),new());
        public void ValidateTemplate(byte[] bytes,string kind)=>renderer.ValidateTemplate(bytes,kind);
        public Task<Doc.RenderOutput> RenderAsync(byte[] bytes,Doc.RenderBasis basis,CancellationToken ct)=>Task.FromResult(new Doc.RenderOutput(renderer.Fill(bytes,basis),"%PDF-1.7 transport fixture"u8.ToArray(),"HTTP-fixture"));
    }
}
