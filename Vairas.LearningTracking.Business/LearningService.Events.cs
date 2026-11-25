using System.Text.Json;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private static readonly string[] SchedulingEvents = ["LessonCompleted", "LessonParticipantsChanged"];
    private static readonly string[] SchoolEvents = ["StudentEnrollmentStarted", "StudentEnrollmentCompleted", "StudentEnrollmentEnded", "SchoolLearningAuthorityChanged"];
    private static void ValidateMessage(IntegrationMessage message)
    {
        if (message.EventId == Guid.Empty || message.SchoolId == Guid.Empty || message.AggregateId == Guid.Empty || message.AggregateVersion < 1 || message.EventVersion != 1 || message.OccurredAt == default ||
            string.IsNullOrWhiteSpace(message.CorrelationId) || !(message.Producer == "scheduling" && SchedulingEvents.Contains(message.EventType) || message.Producer == "school" && SchoolEvents.Contains(message.EventType)))
            throw LearningErrors.Invalid("INVALID_INTEGRATION_MESSAGE");
    }
    private static string MessageFingerprint(IntegrationMessage m) => Hash(Wire.Save(m with { CorrelationId = "" }));
    private static async Task<bool> Received(ILearningSession s, IntegrationMessage m, CancellationToken ct)
    {
        var prior = await s.FindAsync<InboxMessage>(x => x.Id == m.EventId, ct);
        if (prior is null) return false;
        if (prior.Fingerprint != MessageFingerprint(m)) throw LearningErrors.Conflict("EVENT_ID_REUSED");
        return true;
    }
    private void Inbox(ILearningSession s, IntegrationMessage m) => s.Add(new InboxMessage { Id = m.EventId, SchoolId = m.SchoolId, Producer = m.Producer, EventType = m.EventType,
        AggregateId = m.AggregateId, AggregateVersion = m.AggregateVersion, Fingerprint = MessageFingerprint(m), ProcessedAt = Now });

    public async Task ConsumeAsync(IntegrationMessage message, CancellationToken ct)
    {
        ValidateMessage(message);
        await using (var read = await store.OpenAsync(message.SchoolId, false, ct)) { if (await Received(read, message, ct)) return; }
        var actor = SystemActor(message.CorrelationId);
        if (message.Producer == "scheduling")
        {
            var source = await owners.SourceAsync(message.SchoolId, message.AggregateId, ct);
            if (source.LessonRevision < message.AggregateVersion) throw LearningErrors.Dependency("OWNER_REVISION_NOT_VISIBLE");
            await Reconcile(message.SchoolId, message.AggregateId, source, actor, ct, message.EventId, message);
        }
        else if (message.EventType == "SchoolLearningAuthorityChanged")
        {
            var authority = await owners.AuthorityAsync(message.SchoolId, Guid.Empty, null, null, null, ct);
            if (authority.SchoolId != message.SchoolId || authority.SchoolRevision < message.AggregateVersion || authority.ValidUntil <= Now) throw LearningErrors.Dependency("INVALID_SCHOOL_REVISION");
            await using var s = await store.OpenAsync(message.SchoolId, true, ct);
            if (await Received(s, message, ct)) return;
            var state = await s.FindAsync<SchoolSourceState>(x => x.Id == message.SchoolId, ct);
            if (state is null || message.AggregateVersion > state.Revision)
                await ObserveSchoolRevision(s, message.SchoolId, authority.SchoolRevision, actor, ct,
                    authority.RevisionEffectScope == "ProspectiveOnly" && authority.RevisionEffectiveAt is not null ? InvalidationScope.ProspectiveOnly : InvalidationScope.HistoricalReviewRequired, authority.RevisionEffectiveAt);
            Inbox(s, message); await s.CommitAsync(ct);
        }
        else
        {
            var authority = await owners.AuthorityAsync(message.SchoolId, Guid.Empty, message.AggregateId, null, null, ct);
            if (authority.EnrollmentRevision < message.AggregateVersion) throw LearningErrors.Dependency("OWNER_REVISION_NOT_VISIBLE");
            await ReconcileEnrollment(message.SchoolId, message.AggregateId, authority, actor, ct, message);
        }
    }
    private async Task ReconcileEnrollment(Guid school, Guid enrollment, LearningAuthority authority, LearningActor actor, CancellationToken ct, IntegrationMessage? message = null)
    {
        if (authority.SchoolId != school || authority.EnrollmentId != enrollment || authority.StudentId is null || authority.ValidUntil <= Now || authority.EnrollmentRevision < 1) throw LearningErrors.Dependency("INVALID_ENROLLMENT_EVIDENCE");
        await using var s = await store.OpenAsync(school, true, ct);
        if (message is not null && await Received(s, message, ct)) return;
        var source = await s.FindAsync<EnrollmentSourceState>(x => x.Id == enrollment, ct);
        if (source is null)
        {
            source = new EnrollmentSourceState { Id = enrollment, SchoolId = school }; s.Add(source);
        }
        if (authority.EnrollmentRevision >= source.Revision)
        {
            var changed = source.Revision > 0 && (source.Revision != authority.EnrollmentRevision || source.Status != authority.EnrollmentStatus);
            source.Revision = authority.EnrollmentRevision; source.Status = authority.EnrollmentStatus; source.VerifiedAt = Now;
            if (await s.FindAsync<StudentTraining>(x => x.Id == enrollment, ct) is null)
                s.Add(new StudentTraining { Id = enrollment, SchoolId = school, StudentId = authority.StudentId.Value, CategoryCode = authority.Category, CreatedAt = Now, DependencyRevision = 1 });
            if (changed) await Touch(s, school, [enrollment], actor, "ENROLLMENT_SOURCE_CHANGED", ct, InvalidationScope.ProspectiveOnly);
        }
        if (message is not null) Inbox(s, message);
        await s.CommitAsync(ct);
    }
    public async Task ReconcileEnrollmentAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        var actor = SystemActor(Guid.NewGuid().ToString("D"));
        var authority = await owners.AuthorityAsync(school, Guid.Empty, enrollment, null, null, ct);
        await ReconcileEnrollment(school, enrollment, authority, actor, ct);
    }
    public async Task ReconcileLessonAsync(Guid school, Guid lesson, CancellationToken ct) => await FetchSource(school, lesson, SystemActor(Guid.NewGuid().ToString("D")), ct);
}
