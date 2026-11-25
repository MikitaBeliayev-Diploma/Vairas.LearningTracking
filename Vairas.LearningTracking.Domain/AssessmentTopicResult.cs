namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentTopicResult : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ResultVersionId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public Guid AssessmentTopicId { get; set; }
    public int Mark { get; set; }
    public string? Comment { get; set; }
}
