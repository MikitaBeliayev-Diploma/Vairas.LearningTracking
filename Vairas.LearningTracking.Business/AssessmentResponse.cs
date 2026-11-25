using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Latest result version and current historical validity.</summary>
/// <param name="ReadinessInvalidatedAt">Authoritative invalidation time of the linked readiness decision, if any; a historical review
/// (<see cref="CorrectAssessmentRequest.ReviewedInvalidationAt"/>) must echo exactly this value.</param>
/// <param name="ReviewedInvalidationAt">Invalidation time already reviewed by this result version, if any.</param>
public sealed record AssessmentResponse(Guid AssessmentId, Guid ResultVersionId, Guid EnrollmentId, Guid AssessmentDefinitionVersionId, int AttemptNumber,
    long Revision, int Version, int Mark, Outcome Outcome, ResultStatus Status, DateTimeOffset AssessedAt, Guid? ReadinessDecisionId,
    DateTimeOffset? ReadinessInvalidatedAt = null, DateTimeOffset? ReviewedInvalidationAt = null);
