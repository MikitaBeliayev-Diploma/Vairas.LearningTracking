namespace Vairas.LearningTracking.Domain;

public sealed class InboxMessage : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Producer { get; set; } = "";
    public string EventType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public long AggregateVersion { get; set; }
    public string Fingerprint { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}
