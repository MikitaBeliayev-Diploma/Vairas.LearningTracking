namespace Vairas.LearningTracking.Domain;

public sealed class LessonSourceSnapshot : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid LessonId { get; set; }
    public int LessonRevision { get; set; }
    public TrainingKind Kind { get; set; }
    public Guid InstructorId { get; set; }
    public Guid? EnrollmentId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? GroupId { get; set; }
    public long? MembershipRevision { get; set; }
    public DateTimeOffset? ActualStart { get; set; }
    public DateTimeOffset? ActualEnd { get; set; }
    public bool PhysicallyCompleted { get; set; }
    public bool SourceValid { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public Guid? SourceEventId { get; set; }
}
