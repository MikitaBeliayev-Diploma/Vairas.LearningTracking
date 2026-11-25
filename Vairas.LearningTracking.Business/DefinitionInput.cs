using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Versioned assessment content and independently versioned prerequisite list.</summary>
public sealed record DefinitionInput(Guid DefinitionId, Guid DefinitionVersionId, string Code, string Name, TrainingKind TrainingKind, AssessmentStage Stage,
    int MinimumMark, int MaximumMark, int PassingMark, bool RequiredForCompletion, Guid[] TopicIds, RequirementInput[] Requirements);
