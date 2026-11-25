namespace Vairas.LearningTracking.Business;

/// <summary>Immutable published programme rule edition and verification provenance.</summary>
public sealed record DocumentEvidenceRuleSource(string Code, string SourceUri, string Edition, string Applicability, Guid? VerifiedBy, DateTimeOffset? VerifiedAt);
