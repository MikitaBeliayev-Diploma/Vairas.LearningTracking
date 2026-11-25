using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;
public sealed class NotificationSourceService(ILearningStore store) : INotificationSourceService
{
    public async Task<NotificationSourceResponse> ReadAsync(Guid school, Guid assessment, CancellationToken ct)
    {
        if (school == Guid.Empty || assessment == Guid.Empty) throw LearningErrors.Invalid();
        await using var session = await store.OpenAsync(school, false, ct);
        var root = await session.FindAsync<Assessment>(x => x.Id == assessment, ct) ?? throw LearningErrors.Missing();
        var latest = (await session.ListAsync<AssessmentResultVersion>(x => x.AssessmentId == assessment, ct)).MaxBy(x => x.Version)
            ?? throw LearningErrors.Dependency("ASSESSMENT_RESULT_MISSING");
        var decision = latest.ReadinessDecisionId is Guid id ? await session.FindAsync<ReadinessDecision>(x => x.Id == id, ct) : null;
        return new(school, assessment, root.Revision, "Assessment", LearningRules.HistoricalStatus(latest, decision).ToString(), null, null,
            [root.EnrollmentId], null, latest.AssessedAt, latest.Outcome.ToString());
    }
}
