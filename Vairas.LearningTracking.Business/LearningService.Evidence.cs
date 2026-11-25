using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private async Task<(TrainingEvidenceItem[] Items, TrainingEvidenceCheck[] Checks)> EvidenceBasis(ILearningSession s, CreditState c, Guid snapshot, EvidenceKind kind, CancellationToken ct)
    {
        var school = c.Training.SchoolId; var enrollment = c.Training.Id;
        var items = new List<TrainingEvidenceItem>(); var checks = new List<TrainingEvidenceCheck>();
        void Check(string code, CheckOutcome outcome, string explanation, string field = "*", Guid? item = null) => checks.Add(new()
        {
            Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot, EnrollmentId = enrollment, CheckCode = code, FieldCode = field,
            CheckScope = item is null ? CheckScope.Snapshot : CheckScope.EvidenceItem, EvidenceItemId = item, Outcome = outcome, Explanation = explanation
        });
        var definitionIds = (await s.ListAsync<AssessmentDefinitionVersion>(x => x.ProgramVersionId == c.Version.Id && x.RequiredForCompletion, ct)).Select(x => x.Id).ToArray();
        var completeAssessments = definitionIds.All(id => c.Results.Any(r => r.Attempt.DefinitionVersionId == id && r.Status == ResultStatus.Recorded && r.Version.Outcome == Outcome.Passed));
        var practicalComplete = c.Practical.All(x => x.Eligible);
        var theoryComplete = c.Theory.All(x => x.Version.ConfirmedAt is not null && x.Version.Validation == Validation.Valid && (x.Attendance.Attendance == Attendance.Absent || x.Eligible));
        Check("POLICY", c.Policy is null ? CheckOutcome.Unverified : CheckOutcome.Satisfied, c.Policy is null ? "Applicable verified checklist is unavailable." : "Exact configured checklist version verified.");
        Check("SOURCES", c.SourcesVerified ? CheckOutcome.Satisfied : CheckOutcome.Unverified, "Physical sources and programme applicability must be current.");
        Check("PRACTICAL_CONFIRMATIONS", practicalComplete ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied, "Every included practical version requires both participant confirmations and no dispute.");
        Check("THEORY_CONFIRMATIONS", theoryComplete ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied, "Teacher confirmation and the verified full roster are required.");
        Check("RESULT_REVIEWS", c.Results.All(x => x.Status != ResultStatus.ReviewRequired) ? CheckOutcome.Satisfied : CheckOutcome.Unverified, "Unresolved assessment review blocks release.");
        foreach (var p in c.Practical)
        {
            var item = new TrainingEvidenceItem { Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot, EnrollmentId = enrollment, LessonId = p.Record.LessonId,
                PracticalRecordVersionId = p.Version.Id, RecordResponseSequence = p.Responses.Length == 0 ? 0 : p.Responses.Max(x => x.Sequence), CreditedMinutesAtSnapshot = p.Eligible ? p.Version.TrainingMinutes : 0,
                ObservedState = p.Eligible ? "Confirmed" : p.Responses.Any(x => x.Response == ResponseKind.Dispute) ? "Disputed" : "Pending", Explanation = "Exact practical version and response sequence observed at preparation." };
            items.Add(item);
            foreach (var field in c.Policy?.PracticalFields ?? [])
            {
                var valid = field switch
                {
                    "Identity" => p.Record.EnrollmentId == enrollment && p.Version.ProgramVersionId == c.Training.ProgramVersionId,
                    "TeachingMinutes" => p.Version.TrainingMinutes > 0,
                    "Distance" => p.Version.DistanceKm >= 0,
                    "Topics" => (await s.ListAsync<PracticalRecordTopic>(x => x.RecordVersionId == p.Version.Id, ct)).Count > 0,
                    "ActualInterval" => p.Eligible,
                    _ => (bool?)null
                };
                Check("PRACTICAL_FIELD", valid is null ? CheckOutcome.Unverified : valid.Value ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied, valid is null ? "No verified evaluator exists for this required field." : "Required practical fact checked.", field, item.Id);
            }
        }
        foreach (var t in c.Theory) items.Add(new TrainingEvidenceItem { Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot, EnrollmentId = enrollment, LessonId = t.Record.LessonId,
            TheoryAttendanceId = t.Attendance.Id, CreditedMinutesAtSnapshot = t.Eligible ? t.Version.TrainingMinutes : 0, ObservedState = t.Attendance.Attendance.ToString(), Explanation = "Exact own-enrollment attendance row; other learners are excluded." });
        foreach (var r in c.Results) items.Add(new TrainingEvidenceItem { Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot, EnrollmentId = enrollment,
            ResultVersionId = r.Version.Id, ObservedState = r.Status.ToString(), Explanation = "Latest version of this performed attempt; historical validity is evaluated separately." });
        foreach (var code in c.Policy?.JournalChecks ?? [])
        {
            bool? satisfied = code switch
            {
                "Programme" => c.Policy is not null,
                "Sources" => c.SourcesVerified,
                "ConfirmedPracticalRecords" => practicalComplete,
                "ConfirmedTheoryAttendance" => theoryComplete,
                "NoPendingReviews" => c.Results.All(x => x.Status != ResultStatus.ReviewRequired),
                "TrainingMinutes" => c.TheoryMinutes >= c.Version.RequiredTheoryMinutes && c.PracticalMinutes >= c.Version.RequiredPracticalMinutes,
                "RequiredAssessments" => completeAssessments,
                _ => null
            };
            Check("CONFIGURED_CHECK", satisfied is null ? CheckOutcome.Unverified : satisfied.Value ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied,
                satisfied is null ? "Unsupported mandatory policy check requires verified implementation." : "Configured mandatory checklist condition evaluated.", code);
        }
        if (kind == EvidenceKind.CompletionReport)
        {
            Check("COMPLETION_MINUTES", c.TheoryMinutes >= c.Version.RequiredTheoryMinutes && c.PracticalMinutes >= c.Version.RequiredPracticalMinutes ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied, "Both configured training requirements must be satisfied.");
            Check("COMPLETION_ASSESSMENTS", completeAssessments ? CheckOutcome.Satisfied : CheckOutcome.Unsatisfied, "All configured required assessments need eligible passed results.");
        }
        return (items.ToArray(), checks.ToArray());
    }
    private static EvidenceResponse EvidenceDto(TrainingEvidenceSnapshot snapshot, IEnumerable<TrainingEvidenceCheck> checks, bool current) => new(snapshot.Id, snapshot.EnrollmentId, snapshot.ProgramVersionId,
        snapshot.Kind, snapshot.Status, snapshot.DependencyRevision, snapshot.ChecklistVersionId, current,
        checks.OrderBy(x => x.CheckCode).ThenBy(x => x.FieldCode).Select(x => new EvidenceCheckResult(x.CheckCode, x.FieldCode, x.CheckScope, x.EvidenceItemId, x.Outcome, x.Explanation)).ToArray(), snapshot.SchoolId);
    public async Task<CommandResult<EvidenceResponse>> PrepareEvidenceAsync(Guid school, Guid enrollment, PrepareEvidenceRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Kind)) throw LearningErrors.Invalid();
        var authority = await Authority(school, actor, enrollment, ct); Admin(authority);
        await RefreshSources(school, enrollment, actor, ct);
        return await Command(school, operation, "PrepareEvidence", enrollment, request, actor, authority, async s =>
        {
            var c = await Credit(s, enrollment, ct);
            var snapshot = new TrainingEvidenceSnapshot { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = enrollment, ProgramVersionId = c.Version.Id, Kind = request.Kind,
                ChecklistVersionId = c.Policy?.VersionId, DependencyRevision = c.Training.DependencyRevision, PreparedBy = actor.UserId, PreparedAt = Now, SourceVerifiedAt = c.SourcesVerified ? Now : null };
            var basis = await EvidenceBasis(s, c, snapshot.Id, request.Kind, ct);
            if (request.Kind == EvidenceKind.CompletionReport && authority.EnrollmentStatus is not ("Active" or "Completed"))
                basis.Checks = basis.Checks.Append(new TrainingEvidenceCheck { Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot.Id, EnrollmentId = enrollment, CheckCode = "ENROLLMENT_STATUS", FieldCode = "*", CheckScope = CheckScope.Snapshot,
                    Outcome = CheckOutcome.Unsatisfied, Explanation = "Enrollment is not eligible for a current completion report." }).ToArray();
            snapshot.Status = basis.Checks.All(x => x.Outcome == CheckOutcome.Satisfied) ? EvidenceStatus.Ready : EvidenceStatus.Incomplete;
            s.Add(snapshot); foreach (var item in basis.Items) s.Add(item); foreach (var check in basis.Checks) s.Add(check);
            return EvidenceDto(snapshot, basis.Checks, true);
        }, ct);
    }
    private async Task<EvidenceResponse> CurrentEvidence(ILearningSession s, TrainingEvidenceSnapshot snapshot, CancellationToken ct)
    {
        var c = await Credit(s, snapshot.EnrollmentId, ct);
        var checks = await s.ListAsync<TrainingEvidenceCheck>(x => x.SnapshotId == snapshot.Id, ct);
        var currentBasis = await EvidenceBasis(s, c, snapshot.Id, snapshot.Kind, ct);
        var current = snapshot.DependencyRevision == c.Training.DependencyRevision && snapshot.ChecklistVersionId == c.Policy?.VersionId && c.SourcesVerified &&
            checks.All(x => x.Outcome == CheckOutcome.Satisfied) && currentBasis.Checks.All(x => x.Outcome == CheckOutcome.Satisfied);
        return EvidenceDto(snapshot, checks, current);
    }
    public async Task<EvidenceResponse> EvidenceAsync(Guid school, Guid snapshotId, LearningActor actor, CancellationToken ct)
    {
        TrainingEvidenceSnapshot snapshot;
        await using (var read = await store.OpenAsync(school, false, ct)) snapshot = await Required<TrainingEvidenceSnapshot>(read, snapshotId, ct);
        var authority = await Authority(school, actor, snapshot.EnrollmentId, ct); await RefreshSources(school, snapshot.EnrollmentId, actor, ct); Admission(actor);
        await using var s = await store.OpenAsync(school, false, ct);
        var result = await CurrentEvidence(s, await Required<TrainingEvidenceSnapshot>(s, snapshotId, ct), ct);
        return result.Kind == EvidenceKind.CompletionReport && authority.EnrollmentStatus is not ("Active" or "Completed") ? result with { Current = false } : result;
    }
    public async Task<CommandResult<ReportingDeclarationResponse>> DeclareAsync(Guid school, Guid enrollment, ReportingDeclarationRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.SnapshotId); Text(request.Statement, 2000);
        var authority = await Authority(school, actor, enrollment, ct); Admin(authority);
        if (authority.EnrollmentStatus is not ("Active" or "Completed")) throw LearningErrors.Rule("ENROLLMENT_NOT_COMPLETABLE");
        await RefreshSources(school, enrollment, actor, ct);
        return await Command(school, operation, "DeclareManualReporting", enrollment, request, actor, authority, async s =>
        {
            var snapshot = await Required<TrainingEvidenceSnapshot>(s, request.SnapshotId, ct);
            if (snapshot.EnrollmentId != enrollment || snapshot.Kind != EvidenceKind.CompletionReport || snapshot.Status != EvidenceStatus.Ready || !(await CurrentEvidence(s, snapshot, ct)).Current)
                throw LearningErrors.Rule("CURRENT_READY_COMPLETION_REPORT_REQUIRED");
            var declaration = new ReportingDeclaration { Id = Guid.NewGuid(), SchoolId = school, SnapshotId = snapshot.Id, ActorId = actor.UserId, DeclaredAt = Now, Statement = request.Statement, CommandId = operation };
            s.Add(declaration); return new ReportingDeclarationResponse(declaration.Id, snapshot.Id, declaration.DeclaredAt);
        }, ct);
    }
    /// <summary>Administrator-only history of manual reporting declarations for one enrollment, newest first.</summary>
    public async Task<ReportingDeclarationDetail[]> DeclarationsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        var authority = await Authority(school, actor, enrollment, ct); Admin(authority); Admission(actor);
        await using var s = await store.OpenAsync(school, false, ct);
        var snapshots = (await s.ListAsync<TrainingEvidenceSnapshot>(x => x.EnrollmentId == enrollment && x.Kind == EvidenceKind.CompletionReport, ct)).Select(x => x.Id).ToHashSet();
        var declarations = await s.ListAsync<ReportingDeclaration>(x => snapshots.Contains(x.SnapshotId), ct);
        return declarations.OrderByDescending(x => x.DeclaredAt).ThenBy(x => x.Id)
            .Select(x => new ReportingDeclarationDetail(x.Id, x.SnapshotId, x.ActorId, x.DeclaredAt, x.Statement)).ToArray();
    }
    private async Task<LearningAuthority> InternalEnrollment(Guid school, Guid enrollment, CancellationToken ct)
    {
        var authority = await owners.AuthorityAsync(school, Guid.Empty, enrollment, null, null, ct);
        if (!authority.Allowed || authority.SchoolId != school || authority.EnrollmentId != enrollment || authority.ValidUntil <= Now) throw LearningErrors.Dependency("INVALID_ENROLLMENT_EVIDENCE");
        return authority;
    }
    public async Task<ConfirmedPracticalRecordsResponse> ConfirmedAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        await InternalEnrollment(school, enrollment, ct);
        if (!await RefreshSources(school, enrollment, SystemActor(Guid.NewGuid().ToString("D")), ct)) throw LearningErrors.Dependency("SOURCE_UNAVAILABLE");
        await using var s = await store.OpenAsync(school, false, ct); var c = await Credit(s, enrollment, ct);
        if (!c.SourcesVerified) throw LearningErrors.Dependency("CURRENT_EVIDENCE_UNAVAILABLE");
        return new(enrollment, $"training-{c.Training.DependencyRevision}", c.Practical.Where(x => x.Eligible).Select(x => new ConfirmedPracticalRecord(x.Record.Id, x.Record.Revision, x.Version.TrainingMinutes!.Value)).ToArray());
    }
    public async Task<EvidenceResponse> CompletionAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        var authority = await InternalEnrollment(school, enrollment, ct); await RefreshSources(school, enrollment, SystemActor(Guid.NewGuid().ToString("D")), ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var latest = (await s.ListAsync<TrainingEvidenceSnapshot>(x => x.EnrollmentId == enrollment && x.Kind == EvidenceKind.CompletionReport, ct)).MaxBy(x => x.PreparedAt) ?? throw LearningErrors.Missing();
        var result = await CurrentEvidence(s, latest, ct);
        return authority.EnrollmentStatus is "Active" or "Completed" ? result : result with { Current = false };
    }
}
