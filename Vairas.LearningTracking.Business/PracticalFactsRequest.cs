using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Practical facts against an exact verified source snapshot.</summary>
public sealed record PracticalFactsRequest(Guid SchoolId, Guid EnrollmentId, Guid LessonId, Guid ProgramVersionId, Guid SourceSnapshotId,
    int TrainingMinutes, decimal DistanceKm, Guid[] TopicIds, string? DurationExplanation = null, long? ExpectedRevision = null, string? CorrectionReason = null);
