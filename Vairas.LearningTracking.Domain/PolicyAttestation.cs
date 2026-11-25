namespace Vairas.LearningTracking.Domain;

public sealed class PolicyAttestation : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Checksum { get; set; } = "";
    public DateTimeOffset AttestedAt { get; set; }
}
