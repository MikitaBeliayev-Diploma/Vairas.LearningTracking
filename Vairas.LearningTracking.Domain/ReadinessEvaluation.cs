namespace Vairas.LearningTracking.Domain;

public sealed class ReadinessEvaluation : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ReadinessId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public long DependencyRevision { get; set; }
    public FormalStatus FormalStatus { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
    public Guid EvaluatedBy { get; set; }
    public string Explanation { get; set; } = "";
}
