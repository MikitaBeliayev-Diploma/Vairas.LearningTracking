using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;

namespace Vairas.LearningTracking.Configuration;

/// <summary>Application services; infrastructure registrations live in the dedicated Configure* classes.</summary>
public static class ConfigureDependencyInjection
{
    public static IServiceCollection AddLearningServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IChecklistRegistry, ChecklistRegistry>();
        services.AddScoped<INotificationSourceService, NotificationSourceService>();
        services.AddScoped<LearningService>();
        services.AddScoped<ILearningService>(provider => provider.GetRequiredService<LearningService>());
        services.AddScoped<ILearningDocuments>(provider => provider.GetRequiredService<LearningService>());
        services.AddScoped<ILearningIntegrationService>(provider => provider.GetRequiredService<LearningService>());
        services.AddScoped<ILearningOwners, LearningOwners>();
        services.AddScoped<ILearningActorFactory, LearningActorFactory>();
        return services;
    }
}
