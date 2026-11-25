namespace Vairas.LearningTracking.Business;

/// <summary>Retained owner-calculated checklist observation.</summary>
public sealed record DocumentEvidenceCheck(string Code, string Field, string Outcome, string Explanation);
