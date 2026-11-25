namespace Vairas.LearningTracking.Domain;

public sealed class SchoolSourceState : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public long Revision { get; set; }
    public DateTimeOffset LastVerifiedAt { get; set; }
}
