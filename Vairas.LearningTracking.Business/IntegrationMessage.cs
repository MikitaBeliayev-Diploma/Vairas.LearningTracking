using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Validated at-least-once integration message metadata and unchanged body.</summary>
public sealed record IntegrationMessage(Guid EventId, string EventType, int EventVersion, string Producer, Guid SchoolId, Guid AggregateId, long AggregateVersion,
    DateTimeOffset OccurredAt, string CorrelationId, string Payload);
