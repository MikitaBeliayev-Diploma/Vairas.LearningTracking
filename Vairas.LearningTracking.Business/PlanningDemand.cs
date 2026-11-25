using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Immutable required demand and finite options in the requested resource and time domain.</summary>
public sealed record PlanningDemand(Guid DemandId, TrainingKind Kind, int DurationMinutes, int Count, LessonOption[] Options);
