using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Middleware;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Integrations;


public sealed class LearningOwners(IDependencyTransport transport, IOptions<DependencyOptions> optionsSettings) : ILearningOwners
{
    private readonly DependencyOptions options = optionsSettings.Value;
    public Task<LearningAuthority> AuthorityAsync(Guid school, Guid actor, Guid? enrollment, Guid? teacher, Guid? group, CancellationToken ct) =>
        transport.SendAsync<LearningAuthority>(options.SchoolBaseUrl, "internal/v1/learning/authority", new { schoolId = school, actorUserId = actor, enrollmentId = enrollment, teacherId = teacher, groupId = group }, null, ct);
    public Task<LearningAuthority> ApproverAuthorityAsync(Guid school, Guid actor, Guid enrollment, CancellationToken ct) =>
        transport.SendAsync<LearningAuthority>(options.SchoolBaseUrl, "internal/v1/learning/authority", new { schoolId = school, actorUserId = actor, enrollmentId = enrollment, evidenceRevalidation = true }, null, ct);
    public Task<TrainingSource> SourceAsync(Guid school, Guid lesson, CancellationToken ct) => transport.SendAsync<TrainingSource>(Required(options.SchedulingBaseUrl), $"internal/v1/lessons/{lesson:D}/training-source?schoolId={school:D}", null, null, ct);
    public Task<Roster> RosterAsync(Guid school, Guid group, long revision, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => transport.SendAsync<Roster>(options.SchoolBaseUrl,
        $"internal/v1/groups/{group:D}/participants?schoolId={school:D}&membershipRevision={revision}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", null, null, ct);
    public Task<PlanningResources> ResourcesAsync(Guid school, Guid actor, Guid? enrollment, Guid? group, Guid teacher, CancellationToken ct) => transport.SendAsync<PlanningResources>(options.SchoolBaseUrl,
        "internal/v1/learning/planning-resources", new { schoolId = school, actorUserId = actor, enrollmentId = enrollment, groupId = group, teacherId = teacher }, null, ct);
    public Task<Allocations> AllocationsAsync(Guid school, Guid[] enrollments, Guid[] groups, CancellationToken ct) => transport.SendAsync<Allocations>(Required(options.SchedulingBaseUrl),
        "internal/v1/learning/allocations", new { schoolId = school, enrollmentIds = enrollments, groupIds = groups }, null, ct);
    public Task<AssessmentAppointment> AppointmentAsync(Guid school, Guid appointment, CancellationToken ct) => transport.SendAsync<AssessmentAppointment>(Required(options.SchedulingBaseUrl),
        $"internal/v1/learning/assessment-appointments/{appointment:D}?schoolId={school:D}", null, null, ct);
    private static string Required(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" ? value : throw LearningErrors.Dependency("PROVIDER_NOT_CONFIGURED");
}
