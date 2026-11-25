namespace Vairas.LearningTracking.Domain;

// Accepted implementation amendment. The policy is frozen with its programme version.
public sealed class LessonPlanningPolicy : ISchoolRow
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid ProgramVersionId { get; set; }
    public TrainingKind Kind { get; set; }
    public int LessonMinutes { get; set; }
    public bool AllowShortFinalLesson { get; set; }
    public int MinimumFinalLessonMinutes { get; set; }
}
