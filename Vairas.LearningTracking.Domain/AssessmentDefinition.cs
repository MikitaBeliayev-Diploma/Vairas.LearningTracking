namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentDefinition : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ProgramId { get; set; }
    public string Code { get; set; } = "";
}
