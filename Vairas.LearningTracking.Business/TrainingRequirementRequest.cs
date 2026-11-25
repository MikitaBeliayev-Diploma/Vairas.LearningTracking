using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Scheduling asks whether an additional lesson of <paramref name="Kind"/> addresses an outstanding requirement (US-010).</summary>
public sealed record TrainingRequirementRequest(Guid SchoolId, Guid EnrollmentId, TrainingKind Kind, Guid ActorUserId);
