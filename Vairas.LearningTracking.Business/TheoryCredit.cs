using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

internal sealed record TheoryCredit(TheoryTrainingRecord Record, TheoryRecordVersion Version, TheoryAttendance Attendance, bool Eligible);
