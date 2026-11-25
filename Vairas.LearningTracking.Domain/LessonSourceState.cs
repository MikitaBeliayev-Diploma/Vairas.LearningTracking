namespace Vairas.LearningTracking.Domain;

public sealed class LessonSourceState : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid LessonId { get; set; }
    public Guid CurrentSnapshotId { get; set; }
    public Freshness Freshness { get; set; }
    public DateTimeOffset LastVerifiedAt { get; set; }
    public string? Reason { get; set; }
}
