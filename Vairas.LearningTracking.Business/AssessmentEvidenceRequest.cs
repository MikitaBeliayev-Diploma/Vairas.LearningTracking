using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Readiness lookup requested on behalf of an authenticated Scheduling user.</summary>
public sealed record AssessmentEvidenceRequest(AssessmentAllocationRequest Request, Guid ActorUserId);
