using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Explicit qualified assessor decision; it cannot bypass formal requirements.</summary>
public sealed record ReadinessDecisionRequest(Guid EvaluationId, Decision Decision, string Reason, long ExpectedRevision);
