namespace Vairas.LearningTracking.Domain;

public sealed class TrainingEvidenceCheck : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid EnrollmentId { get; set; }
    public string CheckCode { get; set; } = "";
    public CheckScope CheckScope { get; set; }
    public Guid? EvidenceItemId { get; set; }
    public string FieldCode { get; set; } = "";
    public CheckOutcome Outcome { get; set; }
    public string Explanation { get; set; } = "";
}
