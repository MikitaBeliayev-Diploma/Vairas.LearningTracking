namespace Vairas.LearningTracking.Domain;

public sealed class AdmissionRequirement : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public string Code { get; set; } = "";
    public RequirementKind Kind { get; set; }
    public int? MinimumMinutes { get; set; }
    public Guid? PrerequisiteDefinitionVersionId { get; set; }
    public int? MinimumMark { get; set; }
}
