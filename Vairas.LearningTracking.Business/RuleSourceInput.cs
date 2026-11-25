using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Verified source provenance supplied by an authorized programme administrator.</summary>
public sealed record RuleSourceInput(Guid SourceId, string RuleCode, string SourceUri, string Edition, string Applicability, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string UnitDescription, bool Verified);
