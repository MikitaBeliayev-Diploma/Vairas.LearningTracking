using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Exact current confirmed version and credited teaching duration.</summary>
public sealed record ConfirmedPracticalRecord(Guid RecordId, long Revision, int ActualMinutes);
