using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Vairas.LearningTracking.Persistance;
namespace Vairas.LearningTracking.HealthChecks;

public sealed class DatabaseReadiness(IDbContextFactory<LearningDbContext> factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct); await db.Database.OpenConnectionAsync(ct);
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT NOT rolsuper AND NOT rolbypassrls AND NOT rolcreaterole AND NOT rolcreatedb AND (SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relname IN ('student_training','lesson_source_state','practical_record_version','outbox_message','inbox_message') AND c.relrowsecurity AND pg_get_userbyid(c.relowner)<>current_user)=5 FROM pg_roles WHERE rolname=current_user";
            return await command.ExecuteScalarAsync(ct) is true && !(await db.Database.GetPendingMigrationsAsync(ct)).Any() ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database roles, RLS or migrations are not ready.");
        }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException) { return HealthCheckResult.Unhealthy("Local database is unavailable."); }
    }
}
