namespace Vairas.LearningTracking.Domain;

public sealed class DocumentReleaseDecision : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ActorId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Status { get; set; } = "";
    public long EvaluatedRevision { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public string Reason { get; set; } = "";
}
