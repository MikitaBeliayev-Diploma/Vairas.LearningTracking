using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Middleware;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Configuration.Options;

namespace Vairas.LearningTracking.Security;


public sealed class EntryHandler(IOptions<JwtOptions> settings) : AuthorizationHandler<EntryRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EntryRequirement requirement)
    {
        if (context.Resource is not HttpContext http)
            return;
        if (!http.Request.Path.StartsWithSegments("/internal"))
        {
            if ((await http.AuthenticateAsync("User")).Succeeded)
                context.Succeed(requirement);
            return;
        }
        var result = await http.AuthenticateAsync("Workload");
        if (!result.Succeeded || result.Principal is null)
            return;
        var client = result.Principal.FindFirstValue("sub");
        var required = requirement.Scope ?? (http.Request.Path.StartsWithSegments("/internal/v1/delegated") ? "scheduling.mcp.write" : "learning.evidence.read");
        var scopes = result.Principal.FindAll("scope").SelectMany(x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (client is not null && settings.Value.WorkloadClients.TryGetValue(client, out var allowed) &&
            allowed.Contains(required, StringComparer.Ordinal) && scopes.Contains(required, StringComparer.Ordinal))
            context.Succeed(requirement);
    }
}
