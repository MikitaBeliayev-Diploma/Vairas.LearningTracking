namespace Vairas.LearningTracking.Domain;

public sealed class EnrollmentSourceState : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public long Revision { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
}
