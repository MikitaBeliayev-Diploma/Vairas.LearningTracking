namespace Vairas.LearningTracking.Domain;

public sealed class ReadinessEvidenceBasis : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? LessonId { get; set; }
    public Guid? PracticalRecordVersionId { get; set; }
    public Guid? TheoryAttendanceId { get; set; }
    public Guid? ResultVersionId { get; set; }
    public int? ObservedMinutes { get; set; }
}
