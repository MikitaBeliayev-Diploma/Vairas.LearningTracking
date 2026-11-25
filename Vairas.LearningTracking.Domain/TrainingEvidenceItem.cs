namespace Vairas.LearningTracking.Domain;

public sealed class TrainingEvidenceItem : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? PracticalRecordVersionId { get; set; }
    public Guid? TheoryAttendanceId { get; set; }
    public Guid? ResultVersionId { get; set; }
    public Guid? ReadinessDecisionId { get; set; }
    public long? RecordResponseSequence { get; set; }
    public int? CreditedMinutesAtSnapshot { get; set; }
    public string ObservedState { get; set; } = "";
    public string Explanation { get; set; } = "";
}
