namespace Vairas.LearningTracking.Domain;

public sealed class TrainingEvidenceSnapshot : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public EvidenceKind Kind { get; set; }
    public EvidenceStatus Status { get; set; }
    public Guid? ChecklistVersionId { get; set; }
    public long DependencyRevision { get; set; }
    public Guid PreparedBy { get; set; }
    public DateTimeOffset PreparedAt { get; set; }
    public DateTimeOffset? SourceVerifiedAt { get; set; }
}
