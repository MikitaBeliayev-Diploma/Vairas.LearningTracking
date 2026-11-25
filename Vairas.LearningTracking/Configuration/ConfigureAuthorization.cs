using Microsoft.AspNetCore.Authorization;
using Vairas.LearningTracking.Security;

namespace Vairas.LearningTracking.Configuration;

public static class ConfigureAuthorization
{
    private static readonly string[] WorkloadScopes = ["learning.planning.read", "learning.readiness.read", "learning.programmes.read", "learning.evidence.read", "learning.notifications.read", "learning.documents.read", "learning.documents.decide"];

    public static IServiceCollection AddEntryAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("LearningEntry", policy => policy.AddRequirements(new EntryRequirement()));
            foreach (var scope in WorkloadScopes)
                options.AddPolicy(scope, policy => policy.AddRequirements(new EntryRequirement(scope)));
        });
        services.AddScoped<IAuthorizationHandler, EntryHandler>();
        return services;
    }
}
