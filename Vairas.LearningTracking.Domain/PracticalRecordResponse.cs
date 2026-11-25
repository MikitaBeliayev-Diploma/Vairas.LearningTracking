namespace Vairas.LearningTracking.Domain;

public sealed class PracticalRecordResponse : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid RecordId { get; set; }
    public Guid RecordVersionId { get; set; }
    public long Sequence { get; set; }
    public Guid ActorId { get; set; }
    public ResponseRole ActorRole { get; set; }
    public ResponseKind Response { get; set; }
    public DateTimeOffset RespondedAt { get; set; }
    public string? Reason { get; set; }
    public Guid CommandId { get; set; }
}
