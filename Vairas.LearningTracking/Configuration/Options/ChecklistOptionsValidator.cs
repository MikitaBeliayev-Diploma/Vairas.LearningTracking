using Microsoft.Extensions.Options;

namespace Vairas.LearningTracking.Configuration.Options;

public sealed class ChecklistOptionsValidator(IHostEnvironment environment) : IValidateOptions<ChecklistOptions>
{
    public ValidateOptionsResult Validate(string? name, ChecklistOptions options) =>
        !environment.IsEnvironment("Testing") && !environment.IsDevelopment() && options.SyntheticMode
            ? ValidateOptionsResult.Fail("Synthetic policy mode is restricted to development and tests.")
            : ValidateOptionsResult.Success;
}
