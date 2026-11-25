namespace Vairas.LearningTracking.Domain;

public sealed class LearningOperation : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ActorId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Command { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public Guid ResultId { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
}
