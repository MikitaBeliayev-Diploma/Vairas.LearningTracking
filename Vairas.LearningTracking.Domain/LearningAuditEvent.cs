namespace Vairas.LearningTracking.Domain;

public sealed class LearningAuditEvent : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Channel Channel { get; set; }
    public string Action { get; set; } = "";
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public long? TargetVersion { get; set; }
    public string Outcome { get; set; } = "";
    public Guid CorrelationId { get; set; }
    public Guid? CommandId { get; set; }
    public string? Reason { get; set; }
    public Guid? SourceSnapshotId { get; set; }
    public long? DependencyRevision { get; set; }
}
