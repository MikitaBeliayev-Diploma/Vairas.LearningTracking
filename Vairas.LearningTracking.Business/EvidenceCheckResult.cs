using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>One normalized snapshot or evidence-item check.</summary>
public sealed record EvidenceCheckResult(string Code, string Field, CheckScope Scope, Guid? EvidenceItemId, CheckOutcome Outcome, string Explanation);
