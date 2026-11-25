using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
namespace Vairas.LearningTracking.Configuration;
public sealed class ApiDocumentTransformer:IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument d,OpenApiDocumentTransformerContext context,CancellationToken ct)
    {
        d.Info.Title="Vairas Learning Tracking API";d.Info.Version="1.0.0";d.Info.Description="Versioned curricula, confirmed practical records, teacher-confirmed attendance, derived progress, assessor readiness and immutable journal/completion evidence.";
        d.Components??=new();d.Components.SecuritySchemes=new Dictionary<string,IOpenApiSecurityScheme>
        { ["bearerAuth"]=new OpenApiSecurityScheme{Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT"},["workloadBearer"]=new OpenApiSecurityScheme{Type=SecuritySchemeType.Http,Scheme="bearer",BearerFormat="JWT"}};
        foreach(var (path,item) in d.Paths)
        foreach(var operation in item.Operations!.Values)
        {
            var internalRoute=path.StartsWith("/internal/",StringComparison.Ordinal);
            operation.Security=[new(){[new OpenApiSecuritySchemeReference(internalRoute?"workloadBearer":"bearerAuth",d)]=[]}];
            foreach(var parameter in operation.Parameters??[])
                if(parameter.Name=="Idempotency-Key" && parameter is OpenApiParameter key){key.Required=true;key.Description="Stable nonempty UUID; retry the exact command under the same identity.";}
            operation.Responses??=new();
            foreach(var status in new[]{"400","401","403","404","409","422","503"})
                operation.Responses.TryAdd(status,new OpenApiResponse{Description="ProblemDetails with stable code and correlationId; another school's state is never disclosed."});
            if(path.Contains("/delegated/",StringComparison.Ordinal))operation.Description=(operation.Description??"")+" Requires scheduling.mcp.write, an independently validated X-User-Authorization token and an exact X-Confirmation-ID.";
        }
        foreach(var (path,item) in d.Paths)
        foreach(var (method,operation) in item.Operations!)
        {
            foreach(var parameter in operation.Parameters??[])
                if(parameter.Name=="schoolId" && parameter is OpenApiParameter scoped)scoped.Required=true;
            if(!path.StartsWith("/internal/",StringComparison.Ordinal) && (method==HttpMethod.Post || method==HttpMethod.Put) && !path.EndsWith("/source-verifications",StringComparison.Ordinal))
            {
                operation.Parameters??=[];
                operation.Parameters.Add(new OpenApiParameter{Name="Idempotency-Key",In=ParameterLocation.Header,Required=true,Description="Stable nonempty UUID; identical retries reuse the recorded outcome after current authorization.",Schema=new OpenApiSchema{Type=JsonSchemaType.String,Format="uuid"}});
            }
        }
        return Task.CompletedTask;
    }
}
