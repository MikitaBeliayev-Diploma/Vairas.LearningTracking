using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Integrations;


public sealed class OutboxWorker(IServiceScopeFactory scopes, IOptions<ServiceBusOptions> optionsSettings, ILogger<OutboxWorker> logger) : BackgroundService
{
    private readonly ServiceBusOptions options = optionsSettings.Value;
    private static readonly Meter Meter = new("Vairas.LearningTracking");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("learning.outbox.published");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("learning.outbox.failures");
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope(); var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
                var message = await store.ClaimAsync(ct);
                if (message is null) { await Task.Delay(TimeSpan.FromSeconds(2), ct); continue; }
                var sent = false;
                try { await scope.ServiceProvider.GetRequiredService<IEventSender>().SendAsync(message, ct); Published.Add(1); sent = true; }
                catch (Exception e) when (!ct.IsCancellationRequested) { Failed.Add(1); logger.LogWarning("Learning event send failed: {ExceptionType}", e.GetType().Name); }
                await store.FinishAsync(message.Id, message.LeaseId!.Value, sent, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Outbox persistence failed: {ExceptionType}", e.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }
}
