namespace Vairas.LearningTracking.Domain;

public sealed class TrainingTopic : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public string TopicKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}
