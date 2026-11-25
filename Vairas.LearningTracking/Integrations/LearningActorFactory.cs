using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Middleware;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Integrations;


public sealed class LearningActorFactory(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings, TimeProvider clock) : ILearningActorFactory
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public async Task<LearningActor> CreateAsync(HttpContext http, CancellationToken ct)
    {
        var auth = await http.AuthenticateAsync("User");
        if (!auth.Succeeded || auth.Principal is null || !Guid.TryParse(auth.Principal.FindFirstValue("sub"), out var identity) || identity == Guid.Empty ||
            !Guid.TryParse(auth.Principal.FindFirstValue("sid"), out var sessionId) || sessionId == Guid.Empty) throw new UnauthorizedException("INVALID_IDENTITY");
        var start = clock.GetUtcNow();
        var session = await transport.SendAsync<Session>(options.AuthBaseUrl, "internal/v1/sessions/validate", new { identityId = identity, sessionId }, null, ct);
        if (session.IdentityId != identity || session.SessionId != sessionId || session.ValidUntil <= clock.GetUtcNow()) throw LearningErrors.Dependency("INVALID_SESSION_EVIDENCE");
        if (!session.Active) throw new UnauthorizedException("SESSION_INVALID");
        var header = http.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedException("AUTHENTICATION_REQUIRED");
        var profile = Vairas.Delegation.ReadPreviewDelegation.Marked(auth.Principal)
            ? await transport.SendAsync<Profile>(options.UserManagementBaseUrl, "internal/v1/profiles/assistant-actor", new { identityId = identity, sessionId }, null, ct)
            : await transport.SendAsync<Profile>(options.UserManagementBaseUrl, "api/v1/users/me", null, header[7..], ct);
        if (profile.UserId == Guid.Empty || profile.Status != "Active") throw LearningErrors.Denied();
        return new(identity, profile.UserId, profile.StudentId, profile.TeacherId, session.ValidUntil < start.AddSeconds(30) ? session.ValidUntil : start.AddSeconds(30), CorrelationMiddleware.Id(http));
    }
    private sealed record Session(bool Active, Guid IdentityId, Guid SessionId, DateTimeOffset ValidUntil);
    private sealed record Profile(Guid UserId, string Status, Guid? StudentId, Guid? TeacherId);
}
