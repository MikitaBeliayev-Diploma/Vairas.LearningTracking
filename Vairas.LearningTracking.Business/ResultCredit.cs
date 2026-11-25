using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

internal sealed record ResultCredit(Assessment Attempt, AssessmentResultVersion Version, ResultStatus Status);
