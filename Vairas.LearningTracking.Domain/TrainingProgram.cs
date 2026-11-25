namespace Vairas.LearningTracking.Domain;

public sealed class TrainingProgram : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string CategoryCode { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public long Revision { get; set; }
}
