using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Configuration.Options;

public sealed class DependencyOptionsValidator(IHostEnvironment environment) : IValidateOptions<DependencyOptions>
{
    public ValidateOptionsResult Validate(string? name, DependencyOptions options)
    {
        if (environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;
        if (!new[] { options.AuthBaseUrl, options.UserManagementBaseUrl, options.SchoolBaseUrl, options.SchedulingBaseUrl }.All(HttpsOrigin.IsValid))
            return ValidateOptionsResult.Fail("HTTPS dependency origin is required.");
        if (string.IsNullOrWhiteSpace(options.ClientSecret))
            return ValidateOptionsResult.Fail("Configure the external workload secret.");
        return ValidateOptionsResult.Success;
    }
}
