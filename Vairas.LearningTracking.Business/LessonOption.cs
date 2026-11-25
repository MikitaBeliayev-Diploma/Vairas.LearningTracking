using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Bounded slot option; it grants no reservation or vehicle eligibility.</summary>
public sealed record LessonOption(Guid SchoolId, Guid BranchId, Guid TeacherId, string Category, DateTimeOffset StartsAt, DateTimeOffset EndsAt,
    Guid? EnrollmentId = null, Guid? StudentId = null, Guid? VehicleId = null, Guid? GroupId = null);
