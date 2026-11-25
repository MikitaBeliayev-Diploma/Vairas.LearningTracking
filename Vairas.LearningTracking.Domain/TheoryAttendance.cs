namespace Vairas.LearningTracking.Domain;

public sealed class TheoryAttendance : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid RecordVersionId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid StudentId { get; set; }
    public Attendance Attendance { get; set; }
}
