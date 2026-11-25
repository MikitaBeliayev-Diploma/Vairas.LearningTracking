namespace Vairas.LearningTracking.Business;

/// <summary>Durable historical owner decision; no reusable release token.</summary>
public sealed record DocumentReleaseResponse(Guid OperationId, string Status, long EvaluatedRevision, DateTimeOffset DecidedAt, string Reason);
