using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Scheduling supplies its authenticated resolved administrator identity.</summary>
public sealed record PlanningInputsRequest(DraftInputs Inputs, Guid ActorUserId);
