namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentTopic : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public Guid TopicId { get; set; }
}
