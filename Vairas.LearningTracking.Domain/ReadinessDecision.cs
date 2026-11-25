namespace Vairas.LearningTracking.Domain;

public sealed class ReadinessDecision : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ReadinessId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public Decision Decision { get; set; }
    public string Reason { get; set; } = "";
    public string AuthorityBasis { get; set; } = "";
    public Guid CommandId { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
    public Guid? InvalidatedBy { get; set; }
    public string? InvalidationReason { get; set; }
    public long? InvalidationDependencyRevision { get; set; }
    public InvalidationScope? InvalidationScope { get; set; }
    public DateTimeOffset? InvalidationEffectiveAt { get; set; }
    public Guid EnrollmentId { get; set; }
}
