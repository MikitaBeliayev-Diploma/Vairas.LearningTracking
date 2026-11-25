using Npgsql;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration.Options;
using Microsoft.Extensions.Options;
namespace Vairas.LearningTracking.Integrations;


public sealed class ReconciliationWorker(IServiceScopeFactory scopes, IOptions<DatabaseOptions> database, IOptions<ReconciliationOptions> optionsSettings, ILogger<ReconciliationWorker> logger) : BackgroundService
{
    private readonly ReconciliationOptions options = optionsSettings.Value;
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Enabled) return;
        Guid lastSchool = Guid.Empty, lastEnrollment = Guid.Empty;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var sources = new List<(Guid School, Guid Lesson)>(); var enrollments = new List<(Guid School, Guid Enrollment)>();
                await using (var db = new NpgsqlConnection(database.Value.Delivery))
                {
                    await db.OpenAsync(ct);
                    await using var command = db.CreateCommand();
                    command.CommandText = "SELECT school_id,lesson_id FROM lesson_source_state ORDER BY last_verified_at,school_id,lesson_id LIMIT @take";
                    command.Parameters.AddWithValue("take", Math.Clamp(options.BatchSize, 1, 500));
                    await using (var reader = await command.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) sources.Add((reader.GetGuid(0), reader.GetGuid(1)));
                    command.CommandText = "SELECT school_id,id FROM student_training WHERE (school_id,id)>(@school,@id) ORDER BY school_id,id LIMIT @take";
                    command.Parameters.AddWithValue("school", lastSchool); command.Parameters.AddWithValue("id", lastEnrollment);
                    await using var records = await command.ExecuteReaderAsync(ct); while (await records.ReadAsync(ct)) enrollments.Add((records.GetGuid(0), records.GetGuid(1)));
                }
                foreach (var source in sources)
                {
                    try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ILearningIntegrationService>().ReconcileLessonAsync(source.School, source.Lesson, ct); }
                    catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning("Source reconciliation failed: {ExceptionType}", e.GetType().Name); }
                }
                foreach (var enrollment in enrollments)
                {
                    try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ILearningIntegrationService>().ReconcileEnrollmentAsync(enrollment.School, enrollment.Enrollment, ct); }
                    catch (Exception e) when (!ct.IsCancellationRequested) { logger.LogWarning("Enrollment reconciliation failed: {ExceptionType}", e.GetType().Name); }
                    lastSchool = enrollment.School; lastEnrollment = enrollment.Enrollment;
                }
                if (enrollments.Count == 0) { lastSchool = Guid.Empty; lastEnrollment = Guid.Empty; }
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.IntervalSeconds, 5, 3600)), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Reconciliation queue failed: {ExceptionType}", e.GetType().Name); await Task.Delay(TimeSpan.FromSeconds(10), ct); }
        }
    }
}
