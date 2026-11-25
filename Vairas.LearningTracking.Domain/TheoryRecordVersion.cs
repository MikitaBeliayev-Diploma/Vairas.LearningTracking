namespace Vairas.LearningTracking.Domain;

public sealed class TheoryRecordVersion : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid RecordId { get; set; }
    public Guid LessonId { get; set; }
    public int Version { get; set; }
    public Guid SourceSnapshotId { get; set; }
    public int TrainingMinutes { get; set; }
    public string? DurationExplanation { get; set; }
    public Validation Validation { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? CorrectionReason { get; set; }
}
