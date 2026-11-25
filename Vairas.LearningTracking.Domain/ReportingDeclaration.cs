namespace Vairas.LearningTracking.Domain;

public sealed class ReportingDeclaration : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid ActorId { get; set; }
    public DateTimeOffset DeclaredAt { get; set; }
    public string Statement { get; set; } = "";
    public Guid CommandId { get; set; }
}
