namespace Vairas.LearningTracking.Business;

/// <summary>Exact fact references expanded without another learner's theory attendance.</summary>
public sealed record DocumentEvidenceRow(string Kind, Guid ItemId, Guid? LessonId, string State, int CreditedMinutes, DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd,
    decimal? DistanceKm, string[] Topics, string[] Confirmations, string? Outcome, int? Mark, DateTimeOffset? AssessedAt = null, Guid? FactVersionId = null, Guid? SourceSnapshotId = null);
