using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Compare-and-set revision for a lifecycle action.</summary>
public sealed record RevisionRequest(long ExpectedRevision, string? Reason = null);
