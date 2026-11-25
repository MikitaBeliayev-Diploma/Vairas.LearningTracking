using Microsoft.Extensions.Options;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration.Options;

namespace Vairas.LearningTracking.Configuration;

/// <summary>Binds and validates every strongly typed options section; invalid required settings fail at startup.</summary>
public static class ConfigureOptions
{
    /// <summary>Adds the externally supplied checklist policy file (ChecklistFile) as a configuration source.</summary>
    public static ConfigurationManager AddChecklistPolicyFile(this ConfigurationManager configuration)
    {
        var policyPath = configuration["ChecklistFile"];
        if (!string.IsNullOrWhiteSpace(policyPath))
            configuration.AddJsonFile(policyPath, optional: false, reloadOnChange: false);
        return configuration;
    }

    public static IServiceCollection AddLearningOptions(this IServiceCollection services, IConfiguration configuration)
    {
        AddValidated<DatabaseOptions, DatabaseOptionsValidator>(services, configuration.GetSection("ConnectionStrings"));
        AddValidated<JwtOptions, JwtOptionsValidator>(services, configuration.GetSection("Authentication"));
        AddValidated<DependencyOptions, DependencyOptionsValidator>(services, configuration.GetSection("Dependencies"));
        AddValidated<ServiceBusOptions, ServiceBusOptionsValidator>(services, configuration.GetSection("ServiceBus"));
        services.AddOptions<ReconciliationOptions>().Bind(configuration.GetSection("Reconciliation"));
        // Checklist policies are bound with an explicit completeness check (see ChecklistOptions.Bind).
        services.AddOptions<ChecklistOptions>().Configure<IConfiguration>((options, root) =>
        {
            var bound = ChecklistOptions.Bind(root);
            options.SyntheticMode = bound.SyntheticMode;
            options.Policies = bound.Policies;
        }).ValidateOnStart();
        services.AddSingleton<IValidateOptions<ChecklistOptions>, ChecklistOptionsValidator>();
        // The registry takes the plain settings object.
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<ChecklistOptions>>().Value);
        return services;
    }

    private static void AddValidated<TOptions, TValidator>(IServiceCollection services, IConfigurationSection section)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
    }
}
