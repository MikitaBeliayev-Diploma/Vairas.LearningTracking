using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Planning basis version includes curriculum, learning dependencies, School evidence and current allocations.</summary>
public sealed record LearningPlan(Guid SchoolId, Guid ProgramVersionId, string Version, bool Complete, PlanningDemand[] Demands);
