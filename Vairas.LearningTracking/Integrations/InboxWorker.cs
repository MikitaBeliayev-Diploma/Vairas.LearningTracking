using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Integrations;


public sealed class InboxWorker(IServiceScopeFactory scopes, IOptions<ServiceBusOptions> optionsSettings, ILogger<InboxWorker> logger) : BackgroundService
{
    private readonly ServiceBusOptions options = optionsSettings.Value;
    public static IntegrationMessage Decode(ServiceBusReceivedMessage message, string expectedProducer)
    {
        string Header(string name) => message.ApplicationProperties.TryGetValue(name, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? "" : "";
        if (!Guid.TryParse(Header("eventId"), out var id) || message.MessageId != id.ToString("D") || message.Subject != Header("eventType") || Header("producer") != expectedProducer ||
            !int.TryParse(Header("eventVersion"), out var version) || !Guid.TryParse(Header("aggregateId"), out var aggregate) || !long.TryParse(Header("aggregateVersion"), out var revision) ||
            !DateTimeOffset.TryParse(Header("occurredAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var occurred)) throw LearningErrors.Invalid("INVALID_EVENT_HEADERS");
        using var body = JsonDocument.Parse(message.Body.ToString());
        if (!body.RootElement.TryGetProperty("schoolId", out var schoolElement) || !schoolElement.TryGetGuid(out var school)) throw LearningErrors.Invalid("INVALID_EVENT_SCHOOL");
        if (expectedProducer == "scheduling" && (!body.RootElement.TryGetProperty("lessonId", out var lesson) || !lesson.TryGetGuid(out var lessonId) || lessonId != aggregate)) throw LearningErrors.Invalid("EVENT_AGGREGATE_MISMATCH");
        if (expectedProducer == "school" && message.Subject != "SchoolLearningAuthorityChanged" && (!body.RootElement.TryGetProperty("enrollmentId", out var e) || !e.TryGetGuid(out var enrollment) || enrollment != aggregate)) throw LearningErrors.Invalid("EVENT_AGGREGATE_MISMATCH");
        if (expectedProducer == "school" && message.Subject == "SchoolLearningAuthorityChanged" && (aggregate != school || !body.RootElement.TryGetProperty("schoolRevision", out var sr) || !sr.TryGetInt64(out var schoolRevision) || schoolRevision != revision)) throw LearningErrors.Invalid("EVENT_AGGREGATE_MISMATCH");
        return new(id, message.Subject, version, expectedProducer, school, aggregate, revision, occurred, Header("correlationId"), message.Body.ToString());
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        await using var client = new ServiceBusClient(options.ConnectionString);
        var processors = new List<ServiceBusProcessor>();
        try
        {
            foreach (var (topic, producer) in new[] { (options.SchedulingTopic, "scheduling"), (options.SchoolTopic, "school") })
            {
                var processor = client.CreateProcessor(topic, options.SubscriptionName, new() { AutoCompleteMessages = false, MaxConcurrentCalls = 4, MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5) });
                processor.ProcessMessageAsync += async args =>
                {
                    try
                    {
                        var message = Decode(args.Message, producer);
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<ILearningIntegrationService>().ConsumeAsync(message, args.CancellationToken);
                        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                    }
                    catch (Exception e) when (!args.CancellationToken.IsCancellationRequested)
                    {
                        logger.LogWarning("Event processing failed: {ExceptionType}; delivery {DeliveryCount}", e.GetType().Name, args.Message.DeliveryCount);
                        if (e is JsonException || e is BadRequestException or ConflictException or UnprocessableEntityException || args.Message.DeliveryCount >= options.MaxDeliveryCount)
                            await args.DeadLetterMessageAsync(args.Message, "LearningProcessingFailed", "Investigate metadata and restore the authoritative provider before redrive.", args.CancellationToken);
                        else await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                    }
                };
                processor.ProcessErrorAsync += error => { logger.LogWarning("Service Bus receive failed: {ExceptionType}", error.Exception.GetType().Name); return Task.CompletedTask; };
                processors.Add(processor); await processor.StartProcessingAsync(ct);
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally { foreach (var processor in processors) await processor.DisposeAsync(); }
    }
}
