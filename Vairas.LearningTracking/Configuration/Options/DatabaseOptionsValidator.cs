using Microsoft.Extensions.Options;
using Npgsql;

namespace Vairas.LearningTracking.Configuration.Options;

/// <summary>Skipped in the Testing environment, where tests supply disposable database roles.</summary>
public sealed class DatabaseOptionsValidator(IHostEnvironment environment, IOptions<ServiceBusOptions> bus, IOptions<ReconciliationOptions> reconciliation)
    : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (environment.IsEnvironment("Testing"))
            return ValidateOptionsResult.Success;
        var connection = new NpgsqlConnectionStringBuilder(options.LearningTracking);
        if (string.IsNullOrWhiteSpace(connection.Host) || connection.Username != "v_learning_tracking_runtime")
            return ValidateOptionsResult.Fail("Configure the restricted Learning runtime connection.");
        if (bus.Value.Enabled && string.IsNullOrWhiteSpace(options.Delivery))
            return ValidateOptionsResult.Fail("Configure broker and restricted delivery credentials.");
        if (reconciliation.Value.Enabled && string.IsNullOrWhiteSpace(options.Delivery))
            return ValidateOptionsResult.Fail("Configure the restricted reconciliation connection.");
        return ValidateOptionsResult.Success;
    }
}
