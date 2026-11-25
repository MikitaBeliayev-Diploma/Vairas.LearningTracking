using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface ILearningOwners
{
    Task<LearningAuthority> AuthorityAsync(Guid school, Guid actor, Guid? enrollment, Guid? teacher, Guid? group, CancellationToken ct);
    Task<LearningAuthority> ApproverAuthorityAsync(Guid school, Guid actor, Guid enrollment, CancellationToken ct) => AuthorityAsync(school, actor, enrollment, null, null, ct);
    Task<TrainingSource> SourceAsync(Guid school, Guid lesson, CancellationToken ct);
    Task<Roster> RosterAsync(Guid school, Guid group, long revision, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<PlanningResources> ResourcesAsync(Guid school, Guid actor, Guid? enrollment, Guid? group, Guid teacher, CancellationToken ct);
    Task<Allocations> AllocationsAsync(Guid school, Guid[] enrollments, Guid[] groups, CancellationToken ct);
    Task<AssessmentAppointment> AppointmentAsync(Guid school, Guid appointment, CancellationToken ct);
}
