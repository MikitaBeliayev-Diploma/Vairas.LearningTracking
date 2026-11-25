using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Evaluate prerequisites for one exact programme-owned assessment definition version.</summary>
public sealed record EvaluateReadinessRequest(Guid AssessmentDefinitionVersionId);
