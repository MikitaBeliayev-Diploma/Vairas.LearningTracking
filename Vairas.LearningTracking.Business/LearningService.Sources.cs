using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private LessonSourceSnapshot SourceRow(TrainingSource x, Guid? eventId = null) => new()
    {
        Id = Guid.NewGuid(), SchoolId = x.SchoolId, LessonId = x.LessonId, LessonRevision = x.LessonRevision, Kind = x.Kind, InstructorId = x.TeacherId,
        EnrollmentId = x.EnrollmentId, StudentId = x.StudentId, VehicleId = x.VehicleId, GroupId = x.GroupId, MembershipRevision = x.GroupMembershipRevision,
        ActualStart = x.ActualStart, ActualEnd = x.ActualEnd, PhysicallyCompleted = x.PhysicallyCompleted, SourceValid = x.SourceValid, CapturedAt = Now, SourceEventId = eventId
    };
    private async Task<Guid[]> SourceEnrollments(ILearningSession s, LessonSourceSnapshot source, CancellationToken ct)
    {
        if (source.EnrollmentId is Guid enrollment) return [enrollment];
        var records = await s.ListAsync<TheoryTrainingRecord>(x => x.LessonId == source.LessonId, ct);
        var ids = records.Select(x => x.Id).ToArray(); var versions = await s.ListAsync<TheoryRecordVersion>(x => ids.Contains(x.RecordId), ct);
        var versionIds = versions.Select(x => x.Id).ToArray();
        return (await s.ListAsync<TheoryAttendance>(x => versionIds.Contains(x.RecordVersionId), ct)).Select(x => x.EnrollmentId).Distinct().ToArray();
    }
    private async Task<LessonSourceSnapshot> Reconcile(Guid school, Guid lesson, TrainingSource incoming, LearningActor actor, CancellationToken ct, Guid? eventId = null, IntegrationMessage? message = null)
    {
        if (incoming.SchoolId != school || incoming.LessonId != lesson || incoming.LessonRevision < 1) throw LearningErrors.Dependency("INVALID_SOURCE_IDENTITY");
        await using var s = await store.OpenAsync(school, true, ct);
        var state = await s.FindAsync<LessonSourceState>(x => x.LessonId == lesson, ct);
        var previous = state is null ? null : await Required<LessonSourceSnapshot>(s, state.CurrentSnapshotId, ct);
        if (message is not null && await Received(s, message, ct)) return previous ?? throw LearningErrors.Dependency();
        // A stale revision cannot poison a newer source even when its old body is malformed.
        if (previous is not null && incoming.LessonRevision < previous.LessonRevision)
        { if (message is not null) { Inbox(s, message); await s.CommitAsync(ct); } return previous; }
        var source = SourceRow(incoming, eventId);
        if (!LearningRules.SourceShape(source) || previous is not null && incoming.LessonRevision == previous.LessonRevision && !LearningRules.SameSource(source, previous))
        {
            if (state is not null)
            {
                state.Freshness = Freshness.ReconciliationPending; state.Reason = "SOURCE_REVISION_CONFLICT";
                await Touch(s, school, await SourceEnrollments(s, previous!, ct), actor, state.Reason, ct);
                Audit(s, school, actor, "SourceHeld", lesson, source: previous!.Id, reason: state.Reason);
                await s.CommitAsync(ct);
            }
            throw LearningErrors.Dependency("SOURCE_RECONCILIATION_REQUIRED");
        }
        if (previous is not null && incoming.LessonRevision == previous.LessonRevision)
        {
            state!.Freshness = Freshness.Current; state.LastVerifiedAt = Now; state.Reason = null;
            if (message is not null) Inbox(s, message);
            await s.CommitAsync(ct); return previous;
        }
        s.Add(source);
        if (state is null)
        {
            state = new LessonSourceState { Id = Guid.NewGuid(), SchoolId = school, LessonId = lesson }; s.Add(state);
        }
        state.CurrentSnapshotId = source.Id; state.Freshness = Freshness.Current; state.LastVerifiedAt = Now; state.Reason = null;
        var affected = await SourceEnrollments(s, source, ct);
        if (previous is not null) affected = affected.Concat(await SourceEnrollments(s, previous, ct)).Distinct().ToArray();
        await Touch(s, school, affected, actor, "SCHEDULING_SOURCE_CHANGED", ct);
        Audit(s, school, actor, "SourceReconciled", lesson, source: source.Id);
        if (message is not null) Inbox(s, message);
        await s.CommitAsync(ct); return source;
    }
    private async Task MarkUnavailable(Guid school, Guid lesson, LearningActor actor, CancellationToken ct)
    {
        await using var s = await store.OpenAsync(school, true, ct);
        var state = await s.FindAsync<LessonSourceState>(x => x.LessonId == lesson, ct);
        if (state is null || state.Freshness == Freshness.Unavailable) return;
        state.Freshness = Freshness.Unavailable; state.Reason = "SOURCE_UNAVAILABLE";
        var previous = await Required<LessonSourceSnapshot>(s, state.CurrentSnapshotId, ct);
        await Touch(s, school, await SourceEnrollments(s, previous, ct), actor, state.Reason, ct);
        Audit(s, school, actor, "SourceUnavailable", lesson, source: previous.Id);
        await s.CommitAsync(ct);
    }
    private async Task<LessonSourceSnapshot> FetchSource(Guid school, Guid lesson, LearningActor actor, CancellationToken ct)
    {
        TrainingSource incoming;
        try { incoming = await owners.SourceAsync(school, lesson, ct); }
        catch (BusinessException e) when (e is ServiceUnavailableException or NotFoundException)
        { await MarkUnavailable(school, lesson, actor, ct); throw LearningErrors.Dependency("SOURCE_UNAVAILABLE"); }
        return await Reconcile(school, lesson, incoming, actor, ct);
    }
    public async Task<LessonSourceSnapshotResult> VerifySourceAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct)
    {
        Id(lesson); Admission(actor);
        var incoming = await owners.SourceAsync(school, lesson, ct);
        var authority = await Authority(school, actor, incoming.EnrollmentId, ct, incoming.TeacherId, incoming.GroupId);
        if (!authority.Administrator && !(authority.Teacher && actor.TeacherId == incoming.TeacherId) && !authority.Self) throw LearningErrors.Missing();
        var source = await Reconcile(school, lesson, incoming, actor, ct);
        return new(source.Id, source.LessonId, source.LessonRevision, Freshness.Current, source.PhysicallyCompleted, source.ActualStart, source.ActualEnd);
    }
    private async Task<LessonSourceSnapshot> ExactSource(ILearningSession s, Guid id, Guid lesson, CancellationToken ct)
    {
        var source = await Required<LessonSourceSnapshot>(s, id, ct);
        var state = await s.FindAsync<LessonSourceState>(x => x.LessonId == lesson, ct) ?? throw LearningErrors.Rule("VERIFIED_SOURCE_REQUIRED");
        if (source.LessonId != lesson || state.CurrentSnapshotId != id || state.Freshness != Freshness.Current) throw LearningErrors.Conflict("SOURCE_STALE");
        return source;
    }
    private async Task<bool> RefreshSources(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        Guid[] lessons;
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            var practical = await read.ListAsync<PracticalTrainingRecord>(x => x.EnrollmentId == enrollment, ct);
            var attendance = await read.ListAsync<TheoryAttendance>(x => x.EnrollmentId == enrollment, ct);
            var ids = attendance.Select(x => x.RecordVersionId).ToArray();
            var theory = await read.ListAsync<TheoryRecordVersion>(x => ids.Contains(x.Id), ct);
            lessons = practical.Select(x => x.LessonId).Concat(theory.Select(x => x.LessonId)).Distinct().Order().ToArray();
        }
        var verified = true;
        foreach (var lesson in lessons)
        {
            try { await FetchSource(school, lesson, actor, ct); }
            catch (ServiceUnavailableException) { verified = false; }
        }
        await RevalidateApprovers(school, enrollment, actor, ct);
        return verified;
    }
    private async Task RevalidateApprovers(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        ReadinessDecision[] active;
        bool policyVerified;
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            active = (await read.ListAsync<ReadinessDecision>(x => x.EnrollmentId == enrollment && x.Decision == Decision.Approved && x.InvalidatedAt == null, ct)).ToArray();
            if (active.Length == 0) return;
            var c = await Credit(read, enrollment, ct); policyVerified = c.SourcesVerified;
        }
        var authorized = policyVerified;
        foreach (var approver in active.Select(x => x.ActorId).Distinct())
        {
            try
            {
                var a = await owners.ApproverAuthorityAsync(school, approver, enrollment, ct);
                if (a.SchoolId != school || a.ActorUserId != approver || a.EnrollmentId != enrollment || !a.Allowed || !a.Assessor || !a.Teacher || a.ValidUntil <= Now) authorized = false;
            }
            catch (BusinessException e) when (e is ForbiddenException or NotFoundException or ServiceUnavailableException) { authorized = false; }
        }
        if (authorized) return;
        await using var s = await store.OpenAsync(school, true, ct);
        if ((await s.ListAsync<ReadinessDecision>(x => x.EnrollmentId == enrollment && x.Decision == Decision.Approved && x.InvalidatedAt == null, ct)).Count > 0)
        {
            await Touch(s, school, [enrollment], actor, "APPROVAL_AUTHORITY_OR_POLICY_UNVERIFIED", ct);
            await s.CommitAsync(ct);
        }
    }
    private static void SameRoster(AttendanceInput[] attendance, Roster roster, LessonSourceSnapshot source)
    {
        if (roster.SchoolId != source.SchoolId || roster.GroupId != source.GroupId || roster.MembershipVersion != source.MembershipRevision || roster.Participants is null || roster.Participants.Length == 0 ||
            attendance is null || attendance.Any(x => x is null || !Enum.IsDefined(x.Attendance)) || attendance.Select(x => x.EnrollmentId).Distinct().Count() != attendance.Length ||
            attendance.Select(x => x.StudentId).Distinct().Count() != attendance.Length || roster.Participants.Select(x => x.EnrollmentId).Distinct().Count() != roster.Participants.Length ||
            !attendance.Select(x => (x.EnrollmentId, x.StudentId)).ToHashSet().SetEquals(roster.Participants.Select(x => (x.EnrollmentId, x.StudentId)))) throw LearningErrors.Rule("ROSTER_MISMATCH");
    }
}
