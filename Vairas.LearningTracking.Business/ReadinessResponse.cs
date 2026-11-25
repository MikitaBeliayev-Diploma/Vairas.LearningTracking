using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Formal prerequisite result and separately recorded human decision.</summary>
/// <param name="DecisionInvalidatedAt">Authoritative invalidation time of the current decision; a historical review must echo it exactly.</param>
public sealed record ReadinessResponse(Guid EvaluationId, Guid ReadinessId, Guid EnrollmentId, Guid AssessmentDefinitionVersionId, long Revision,
    FormalStatus FormalStatus, string Status, RequirementResult[] Requirements, Guid? DecisionId, Decision? Decision, bool Invalidated,
    DateTimeOffset? DecisionInvalidatedAt = null);
