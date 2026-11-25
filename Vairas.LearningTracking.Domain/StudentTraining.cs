namespace Vairas.LearningTracking.Domain;

public sealed class StudentTraining : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public string CategoryCode { get; set; } = "";
    public Guid? ProgramVersionId { get; set; }
    public Guid? ProgramAssignedBy { get; set; }
    public DateTimeOffset? ProgramAssignedAt { get; set; }
    public long DependencyRevision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
