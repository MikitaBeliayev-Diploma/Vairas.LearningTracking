using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Whole-group theory facts, independent of practical dual confirmation.</summary>
public sealed record TheoryFactsRequest(Guid SchoolId, Guid LessonId, Guid SourceSnapshotId, int TrainingMinutes, AttendanceInput[] AttendanceRows,
    string? DurationExplanation = null, long? ExpectedRevision = null, string? CorrectionReason = null);
