using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;

namespace Vairas.LearningTracking.Configuration;

public static class ConfigureMessageBroker
{
    public static IServiceCollection AddMessageBroker(this IServiceCollection services)
    {
        services.AddSingleton<IEventSender, ServiceBusSender>();
        services.AddHostedService<OutboxWorker>();
        services.AddHostedService<InboxWorker>();
        services.AddHostedService<ReconciliationWorker>();
        return services;
    }
}
