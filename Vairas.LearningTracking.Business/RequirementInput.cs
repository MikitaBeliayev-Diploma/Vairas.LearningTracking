using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>A directly owned prerequisite for exactly one assessment definition version.</summary>
public sealed record RequirementInput(Guid RequirementId, string Code, RequirementKind Kind, int? MinimumMinutes = null, Guid? PrerequisiteDefinitionVersionId = null, int? MinimumMark = null);
