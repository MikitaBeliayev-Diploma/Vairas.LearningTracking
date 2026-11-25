namespace Vairas.LearningTracking.Domain;

public sealed class ReadinessRequirementResult : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public Guid RequirementId { get; set; }
    public FormalStatus Status { get; set; }
    public int? ObservedMinutes { get; set; }
    public int? ObservedMark { get; set; }
    public string Explanation { get; set; } = "";
}
