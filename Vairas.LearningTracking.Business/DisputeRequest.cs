using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Dispute closes the exact current fact version for credit.</summary>
public sealed record DisputeRequest(long Revision, string Reason);
