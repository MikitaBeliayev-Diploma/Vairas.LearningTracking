using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Configuration.Options;

public sealed class JwtOptionsValidator(IHostEnvironment environment) : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options) =>
        environment.IsEnvironment("Testing") || HttpsOrigin.IsValid(options.Authority) && HttpsOrigin.IsValid(options.Issuer)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail("HTTPS dependency origin is required.");
}
