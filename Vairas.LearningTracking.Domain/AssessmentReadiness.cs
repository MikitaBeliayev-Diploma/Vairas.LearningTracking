namespace Vairas.LearningTracking.Domain;

public sealed class AssessmentReadiness : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid EnrollmentId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public Guid DefinitionVersionId { get; set; }
    public Guid? CurrentEvaluationId { get; set; }
    public Guid? CurrentDecisionId { get; set; }
    public long Revision { get; set; }
}
