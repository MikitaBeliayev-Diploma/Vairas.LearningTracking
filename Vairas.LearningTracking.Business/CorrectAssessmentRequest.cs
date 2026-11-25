using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Append an authorized correction or historical review outcome without rewriting the performed attempt.</summary>
public sealed record CorrectAssessmentRequest(long ExpectedRevision, int Mark, ResultStatus Status, string CorrectionReason, DateTimeOffset? ReviewedInvalidationAt = null,
    string? Comment = null, TopicMarkInput[]? TopicMarks = null);
