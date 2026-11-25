using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

internal sealed record CreditState(StudentTraining Training, TrainingProgramVersion Version, ChecklistPolicy? Policy, PracticalCredit[] Practical,
    TheoryCredit[] Theory, ResultCredit[] Results, int TheoryMinutes, int PracticalMinutes, bool SourcesVerified);
