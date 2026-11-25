using System.Security.Cryptography;
using System.Text;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService(ILearningStore store, ILearningOwners owners, IChecklistRegistry policies, TimeProvider clock) : ILearningService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")).DateTime);
    private static void Id(Guid value) { if (value == Guid.Empty) throw LearningErrors.Invalid(); }
    private static void Text(string? value, int max = 1000) { if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw LearningErrors.Invalid(); }
    private void Admission(LearningActor actor) { Id(actor.UserId); if (actor.ValidUntil <= Now) throw LearningErrors.Dependency("ADMISSION_EXPIRED"); }
    private async Task<LearningAuthority> Authority(Guid school, LearningActor actor, Guid? enrollment, CancellationToken ct, Guid? teacher = null, Guid? group = null)
    {
        Id(school); Admission(actor);
        var a = await owners.AuthorityAsync(school, actor.UserId, enrollment, teacher ?? actor.TeacherId, group, ct);
        if (a.SchoolId != school || a.ActorUserId != actor.UserId || a.EnrollmentId != enrollment || a.SchoolRevision < 1 || a.ValidUntil <= Now) throw LearningErrors.Dependency("INVALID_SCHOOL_EVIDENCE");
        if (!a.Allowed) throw LearningErrors.Missing();
        Admission(actor); return a;
    }
    private static void Admin(LearningAuthority a) { if (!a.Administrator) throw LearningErrors.Denied(); }
    private static void Active(LearningAuthority a) { if (a.EnrollmentId is not null && a.EnrollmentStatus != "Active") throw LearningErrors.Rule("ENROLLMENT_NOT_ACTIVE"); }
    private static void Assessor(LearningAuthority a) { if (!a.Teacher || !a.Assessor) throw LearningErrors.Denied(); Active(a); }
    private static void Teacher(LearningActor actor, LearningAuthority a, LessonSourceSnapshot source)
    { if (!a.Teacher || actor.TeacherId != source.InstructorId) throw LearningErrors.Denied(); Active(a); }
    private static async Task<T> Required<T>(ILearningSession s, Guid id, CancellationToken ct) where T : class, ISchoolRow =>
        await s.FindAsync<T>(x => x.Id == id, ct) ?? throw LearningErrors.Missing();
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Fingerprint<T>(string command, Guid? target, T payload, LearningActor actor) => Hash(Wire.Save(new { command, target, actor.UserId, actor.Channel, payload }));
    private async Task<CommandResult<TResult>> Command<TRequest, TResult>(Guid school, Guid operation, string command, Guid? target, TRequest payload,
        LearningActor actor, LearningAuthority authority, Func<ILearningSession, Task<TResult>> apply, CancellationToken ct)
    {
        Id(operation); Admission(actor);
        if (authority.ValidUntil <= Now) throw LearningErrors.Dependency("AUTHORITY_EXPIRED");
        var fingerprint = Fingerprint(command, target, payload, actor);
        await using var s = await store.OpenAsync(school, true, ct);
        Admission(actor);
        if (authority.ValidUntil <= Now) throw LearningErrors.Dependency("AUTHORITY_EXPIRED");
        var prior = await s.FindAsync<LearningOperation>(x => x.Id == operation, ct);
        if (prior is not null)
        {
            if (prior.ActorId != actor.UserId || prior.Fingerprint != fingerprint) throw LearningErrors.Conflict("IDEMPOTENCY_KEY_REUSED");
            return new(Wire.Read<TResult>(prior.ResultJson), true);
        }
        await ObserveSchoolRevision(s, school, authority.SchoolRevision, actor, ct,
            authority.RevisionEffectScope == "ProspectiveOnly" && authority.RevisionEffectiveAt is not null ? InvalidationScope.ProspectiveOnly : InvalidationScope.HistoricalReviewRequired, authority.RevisionEffectiveAt);
        var result = await apply(s);
        s.Add(new LearningOperation { Id = operation, SchoolId = school, ActorId = actor.UserId, Fingerprint = fingerprint, Command = command,
            ResultJson = Wire.Save(result), ResultId = target ?? Guid.Empty, AppliedAt = Now });
        Audit(s, school, actor, command, target ?? operation, operation);
        await s.CommitAsync(ct);
        return new(result, false);
    }
    private void Audit(ILearningSession s, Guid school, LearningActor actor, string action, Guid target, Guid? operation = null, string? reason = null, Guid? source = null, long? dependency = null)
    {
        s.Add(new LearningAuditEvent { Id = Guid.NewGuid(), SchoolId = school, ActorId = actor.UserId, OccurredAt = Now, Channel = actor.Channel,
            Action = action, TargetType = "Learning", TargetId = target, Outcome = "Applied", CorrelationId = Guid.TryParse(actor.CorrelationId, out var c) ? c : Guid.NewGuid(),
            CommandId = operation, Reason = reason, SourceSnapshotId = source, DependencyRevision = dependency });
    }
    private void Event<T>(ILearningSession s, Guid school, Guid aggregate, long revision, string type, T payload, LearningActor actor)
    {
        s.Add(new OutboxMessage { Id = Guid.NewGuid(), SchoolId = school, AggregateId = aggregate, AggregateVersion = revision, EventType = type,
            Payload = Wire.Save(payload), CorrelationId = actor.CorrelationId, OccurredAt = Now, NextAttemptAt = Now });
    }
    private async Task Touch(ILearningSession s, Guid school, IEnumerable<Guid> enrollments, LearningActor actor, string reason, CancellationToken ct,
        InvalidationScope scope = InvalidationScope.HistoricalReviewRequired, DateTimeOffset? effective = null, Guid? preserveDecision = null)
    {
        foreach (var enrollment in enrollments.Distinct().Order())
        {
            var training = await s.FindAsync<StudentTraining>(x => x.Id == enrollment, ct);
            if (training is null) continue;
            training.DependencyRevision++;
            foreach (var decision in await s.ListAsync<ReadinessDecision>(x => x.EnrollmentId == enrollment && x.Decision == Decision.Approved && x.InvalidatedAt == null, ct))
            {
                if (decision.Id == preserveDecision) continue;
                decision.InvalidatedAt = Now; decision.InvalidatedBy = actor.UserId; decision.InvalidationReason = reason;
                decision.InvalidationDependencyRevision = training.DependencyRevision;
                decision.InvalidationScope = scope == InvalidationScope.ProspectiveOnly && effective < decision.DecidedAt ? InvalidationScope.HistoricalReviewRequired : scope;
                decision.InvalidationEffectiveAt = scope == InvalidationScope.ProspectiveOnly ? effective ?? Now : effective;
                Audit(s, school, actor, "ReadinessInvalidated", decision.Id, reason: reason, dependency: training.DependencyRevision);
            }
            Event(s, school, enrollment, training.DependencyRevision, "TrainingEvidenceInvalidated", new { schoolId = school, enrollmentId = enrollment, dependencyRevision = training.DependencyRevision, reason }, actor);
        }
    }
    private async Task ObserveSchoolRevision(ILearningSession s, Guid school, long revision, LearningActor actor, CancellationToken ct,
        InvalidationScope scope = InvalidationScope.HistoricalReviewRequired, DateTimeOffset? effective = null)
    {
        var state = await s.FindAsync<SchoolSourceState>(x => x.Id == school, ct);
        if (state is null)
        {
            s.Add(new SchoolSourceState { Id = school, SchoolId = school, Revision = revision, LastVerifiedAt = Now });
            return;
        }
        if (revision < state.Revision) throw LearningErrors.Dependency("SCHOOL_REVISION_STALE");
        if (revision == state.Revision) return;
        if (revision != state.Revision + 1) scope = InvalidationScope.HistoricalReviewRequired;
        state.Revision = revision; state.LastVerifiedAt = Now;
        var trainings = await s.ListAsync<StudentTraining>(x => true, ct);
        await Touch(s, school, trainings.Select(x => x.Id), actor, "SCHOOL_AUTHORITY_CHANGED", ct, scope, effective);
    }
    private async Task<(StudentTraining Training, TrainingProgram Program, TrainingProgramVersion Version, ChecklistPolicy? Policy)> Programme(ILearningSession s, Guid enrollment, CancellationToken ct, bool requireApplicable = true)
    {
        var training = await Required<StudentTraining>(s, enrollment, ct);
        if (training.ProgramVersionId is not Guid versionId) throw LearningErrors.Rule("PROGRAMME_NOT_ASSIGNED");
        var version = await Required<TrainingProgramVersion>(s, versionId, ct);
        var program = await Required<TrainingProgram>(s, version.ProgramId, ct);
        var policy = Policy(program, version);
        if (policy is not null)
        {
            var attestation = await s.FindAsync<PolicyAttestation>(x => x.Id == policy.VersionId, ct);
            if (attestation?.Checksum != Hash(Wire.Save(policy))) policy = null;
        }
        if (requireApplicable && (version.Status == ProgramStatus.Draft || !version.LegalBasisVerified || version.EffectiveFrom > Today || version.EffectiveTo < Today || policy is null))
            throw LearningErrors.Rule("PROGRAMME_NOT_APPLICABLE");
        if (program.CategoryCode != training.CategoryCode) throw LearningErrors.Rule("PROGRAMME_CATEGORY_MISMATCH");
        return (training, program, version, policy);
    }
    private ChecklistPolicy? Policy(TrainingProgram p, TrainingProgramVersion v) => v.JournalChecklistVersionId is Guid id ? policies.Find(id, p.SchoolId, p.CategoryCode, v.PathwayCode, Today) : null;
    internal static LearningActor SystemActor(string correlation) => new(Guid.Empty, Guid.Parse("c0631ba1-81c9-4ce0-9e04-840def1f0a13"), null, null, DateTimeOffset.MaxValue, correlation, Channel.Integration);
}

