namespace Vairas.LearningTracking.Domain;

public sealed class Assessment : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public int AttemptNumber { get; set; }
    public Guid? AppointmentId { get; set; }
    public long Revision { get; set; }
}
