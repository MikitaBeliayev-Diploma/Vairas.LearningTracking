namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentDefinitionVersion : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DefinitionId { get; set; }
    public Guid ProgramId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public string Name { get; set; } = "";
    public TrainingKind TrainingKind { get; set; }
    public AssessmentStage Stage { get; set; }
    public int MinimumMark { get; set; }
    public int MaximumMark { get; set; }
    public int PassingMark { get; set; }
    public bool RequiredForCompletion { get; set; }
}
