namespace Vairas.LearningTracking.Domain;

public sealed class ProgramRuleSource : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public string RuleCode { get; set; } = "";
    public string SourceUri { get; set; } = "";
    public string SourceEdition { get; set; } = "";
    public string Applicability { get; set; } = "";
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string UnitDescription { get; set; } = "";
}
