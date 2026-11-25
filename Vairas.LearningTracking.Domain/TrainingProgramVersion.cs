namespace Vairas.LearningTracking.Domain;

public sealed class TrainingProgramVersion : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ProgramId { get; set; }
    public int Version { get; set; }
    public ProgramStatus Status { get; set; }
    public string PathwayCode { get; set; } = "";
    public int RequiredTheoryMinutes { get; set; }
    public int RequiredPracticalMinutes { get; set; }
    public int TheoryMinutesPerUnit { get; set; }
    public int PracticalMinutesPerUnit { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool LegalBasisVerified { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public Guid? JournalChecklistVersionId { get; set; }
}
