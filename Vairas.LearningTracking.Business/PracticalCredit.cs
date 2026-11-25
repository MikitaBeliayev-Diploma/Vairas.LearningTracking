using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

internal sealed record PracticalCredit(PracticalTrainingRecord Record, PracticalRecordVersion Version, PracticalRecordResponse[] Responses, bool Eligible);
