namespace Vairas.LearningTracking.Domain;

public sealed class PracticalTrainingRecord : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid LessonId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public long Revision { get; set; }
}
