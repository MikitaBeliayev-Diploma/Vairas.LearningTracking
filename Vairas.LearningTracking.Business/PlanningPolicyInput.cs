using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Explicit session length and residual-minute policy; contains no inferred legal values.</summary>
public sealed record PlanningPolicyInput(TrainingKind Kind, int LessonMinutes, bool AllowShortFinalLesson, int MinimumFinalLessonMinutes);
