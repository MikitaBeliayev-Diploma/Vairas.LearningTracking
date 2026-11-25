using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>One independently checked prerequisite.</summary>
public sealed record RequirementResult(Guid RequirementId, FormalStatus Status, int? ObservedMinutes, int? ObservedMark, string Explanation);
