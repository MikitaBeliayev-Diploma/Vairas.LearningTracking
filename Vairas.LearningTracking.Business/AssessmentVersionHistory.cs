using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Read-only assessment history; historical entries deliberately contain no command revision token.</summary>
public sealed record AssessmentVersionHistory(Guid ResultVersionId, int Version, bool Current, int Mark, Outcome Outcome, ResultStatus StoredStatus,
    ResultStatus CurrentValidity, DateTimeOffset AssessedAt, DateTimeOffset RecordedAt, Guid RecordedBy, string? CorrectionReason,
    DateTimeOffset? ReviewedInvalidationAt, TopicMarkInput[] TopicMarks);
