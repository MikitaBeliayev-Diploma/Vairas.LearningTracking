using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Exact Scheduling allocation request for a qualified assessment.</summary>
public sealed record AssessmentAllocationRequest(Guid SchoolId, Guid EnrollmentId, Guid AssessmentDefinitionVersionId, Guid ReadinessEvaluationId, Guid TeacherId,
    Guid BranchId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason, Guid? VehicleId = null);
