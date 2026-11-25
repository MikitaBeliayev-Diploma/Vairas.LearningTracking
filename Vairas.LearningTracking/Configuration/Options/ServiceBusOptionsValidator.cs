using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Configuration.Options;

/// <summary>A disabled broker integration needs no credentials.</summary>
public sealed class ServiceBusOptionsValidator(IHostEnvironment environment) : IValidateOptions<ServiceBusOptions>
{
    public ValidateOptionsResult Validate(string? name, ServiceBusOptions options) =>
        !environment.IsEnvironment("Testing") && options.Enabled && string.IsNullOrWhiteSpace(options.ConnectionString)
            ? ValidateOptionsResult.Fail("Configure broker and restricted delivery credentials.")
            : ValidateOptionsResult.Success;
}
