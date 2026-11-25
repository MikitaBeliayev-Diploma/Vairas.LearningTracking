using Microsoft.Extensions.Options;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration.Options;
using Vairas.LearningTracking.Persistance;

namespace Vairas.LearningTracking.Configuration;

public static class ConfigureDatabase
{
    public static IServiceCollection AddLearningDatabase(this IServiceCollection services)
    {
        services.AddDbContextFactory<LearningDbContext>((provider, builder) =>
            builder.UseLearning(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.LearningTracking));
        services.AddScoped<ILearningStore, LearningStore>();
        // Delivery and reconciliation workers connect with their own, separately granted database role.
        services.AddScoped<IOutboxStore>(provider => new OutboxStore(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.Delivery));
        return services;
    }
}
