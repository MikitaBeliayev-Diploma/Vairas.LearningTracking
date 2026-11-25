using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Integrations;


public sealed class ServiceBusSender(IOptions<ServiceBusOptions> optionsSettings) : IEventSender, IAsyncDisposable
{
    private readonly ServiceBusOptions options = optionsSettings.Value;
    private ServiceBusClient? client;
    public async Task SendAsync(OutboxMessage message, CancellationToken ct)
    {
        client ??= new(options.ConnectionString);
        await using var sender = client.CreateSender(options.TopicName);
        await sender.SendMessageAsync(ToMessage(message), ct);
    }
    public static ServiceBusMessage ToMessage(OutboxMessage message)
    {
        var envelope = new ServiceBusMessage(BinaryData.FromString(message.Payload)) { MessageId = message.Id.ToString("D"), Subject = message.EventType, CorrelationId = message.CorrelationId, ContentType = "application/json" };
        envelope.ApplicationProperties["eventId"] = message.Id.ToString("D"); envelope.ApplicationProperties["eventType"] = message.EventType;
        envelope.ApplicationProperties["eventVersion"] = 1; envelope.ApplicationProperties["occurredAt"] = message.OccurredAt.ToString("O");
        envelope.ApplicationProperties["producer"] = "learning"; envelope.ApplicationProperties["correlationId"] = message.CorrelationId;
        envelope.ApplicationProperties["aggregateId"] = message.AggregateId.ToString("D"); envelope.ApplicationProperties["aggregateVersion"] = message.AggregateVersion;
        return envelope;
    }
    public ValueTask DisposeAsync() => client?.DisposeAsync() ?? ValueTask.CompletedTask;
}
