namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentResultVersion : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid AssessmentId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public int Version { get; set; }
    public Guid CommandId { get; set; }
    public ResultStatus Status { get; set; }
    public Guid AssessorId { get; set; }
    public DateTimeOffset AssessedAt { get; set; }
    public Guid RecordedBy { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public int Mark { get; set; }
    public Outcome Outcome { get; set; }
    public Guid? ReadinessId { get; set; }
    public Guid? ReadinessDecisionId { get; set; }
    public string? Comment { get; set; }
    public string? CorrectionReason { get; set; }
    public DateTimeOffset? ReviewedInvalidationAt { get; set; }
}
