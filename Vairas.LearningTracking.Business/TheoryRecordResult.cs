using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Teacher-reviewed theory record and its exact full roster.</summary>
public sealed record TheoryRecordResult(Guid RecordId, long Revision, int FactVersion, string Status, TheoryFactsRequest Facts, Guid? ConfirmedBy, DateTimeOffset? ConfirmedAt, bool SourceCurrent = false);
