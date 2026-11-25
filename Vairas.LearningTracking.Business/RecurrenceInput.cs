using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Scheduling-owned recurrence request retained only for wire compatibility.</summary>
public sealed record RecurrenceInput(string Frequency, int Interval, int[] Weekdays, TimeOnly LocalStartTime, DateOnly StartsOn, DateOnly? EndsOn, int? OccurrenceCount, Guid DemandId);
