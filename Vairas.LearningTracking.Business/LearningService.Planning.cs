using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService : ILearningIntegrationService
{
    public async Task<ProgrammeApplicabilityResponse> ApplicabilityAsync(ProgrammeApplicabilityRequest request, CancellationToken ct)
    {
        Id(request.SchoolId); Id(request.EnrollmentId); Id(request.GroupId); Id(request.ProgramVersionId);
        var a = await owners.AuthorityAsync(request.SchoolId, Guid.Empty, request.EnrollmentId, null, request.GroupId, ct);
        if (a.SchoolId != request.SchoolId || a.EnrollmentId != request.EnrollmentId || a.GroupId != request.GroupId || a.ValidUntil <= Now || !a.Allowed || a.EnrollmentStatus != "Active") throw LearningErrors.Dependency("INVALID_PROSPECTIVE_ASSOCIATION");
        await using var s = await store.OpenAsync(request.SchoolId, false, ct);
        var v = await Required<TrainingProgramVersion>(s, request.ProgramVersionId, ct); var p = await Required<TrainingProgram>(s, v.ProgramId, ct);
        var policy = Policy(p, v); var training = await s.FindAsync<StudentTraining>(x => x.Id == request.EnrollmentId, ct);
        var attestation = policy is null ? null : await s.FindAsync<PolicyAttestation>(x => x.Id == policy.VersionId, ct);
        var allowed = v.Status == ProgramStatus.Published && v.LegalBasisVerified && v.EffectiveFrom <= Today && !(v.EffectiveTo < Today) && policy is not null &&
            attestation?.Checksum == Hash(Wire.Save(policy)) && p.CategoryCode == a.Category && (training?.ProgramVersionId is null || training.ProgramVersionId == v.Id);
        return new(allowed, request.SchoolId, request.EnrollmentId, request.GroupId, request.ProgramVersionId, a.ValidUntil < Now.AddSeconds(30) ? a.ValidUntil : Now.AddSeconds(30));
    }
    public async Task<TrainingRequirementEvidence> RequirementAsync(TrainingRequirementRequest r, CancellationToken ct)
    {
        Id(r.SchoolId); Id(r.EnrollmentId); Id(r.ActorUserId); if (!Enum.IsDefined(r.Kind)) throw LearningErrors.Invalid();
        var actor = new LearningActor(Guid.Empty, r.ActorUserId, null, null, Now.AddSeconds(30), Guid.NewGuid().ToString("D"), Channel.Integration);
        var authority = await Authority(r.SchoolId, actor, r.EnrollmentId, ct); Admin(authority); Active(authority);
        await using (var read = await store.OpenAsync(r.SchoolId, false, ct))
            if ((await read.FindAsync<StudentTraining>(x => x.Id == r.EnrollmentId, ct))?.ProgramVersionId is null)
                return new(r.SchoolId, r.EnrollmentId, r.Kind, false, Now.AddSeconds(20));
        await RefreshSources(r.SchoolId, r.EnrollmentId, actor, ct);
        await using var s = await store.OpenAsync(r.SchoolId, false, ct);
        var c = await Credit(s, r.EnrollmentId, ct);
        // Applicable requirements of this kind: the confirmed-minute requirement, or a required assessment of the same kind that is
        // not yet passed (an additional lesson may prepare for it). Unverified sources/policy leave the requirement outstanding:
        // Learning never declares training complete without verified evidence.
        var minutesOutstanding = r.Kind == TrainingKind.Practical
            ? c.PracticalMinutes < c.Version.RequiredPracticalMinutes
            : c.TheoryMinutes < c.Version.RequiredTheoryMinutes;
        var assessments = await s.ListAsync<AssessmentDefinitionVersion>(x => x.ProgramVersionId == c.Version.Id && x.RequiredForCompletion && x.TrainingKind == r.Kind, ct);
        var assessmentOutstanding = assessments.Any(d => !c.Results.Any(x => x.Attempt.DefinitionVersionId == d.Id && x.Status == ResultStatus.Recorded && x.Version.Outcome == Outcome.Passed));
        var outstanding = !c.SourcesVerified || minutesOutstanding || assessmentOutstanding;
        return new(r.SchoolId, r.EnrollmentId, r.Kind, outstanding, Now.AddSeconds(20));
    }
    public async Task<AssessmentEvidence> AssessmentEvidenceAsync(AssessmentEvidenceRequest value, CancellationToken ct)
    {
        var r = value.Request; Id(value.ActorUserId); Id(r.SchoolId); Id(r.EnrollmentId);
        var actor = new LearningActor(Guid.Empty, value.ActorUserId, null, null, Now.AddSeconds(30), Guid.NewGuid().ToString("D"), Channel.Integration);
        var authority = await Authority(r.SchoolId, actor, r.EnrollmentId, ct, r.TeacherId); Admin(authority); Active(authority);
        await RefreshSources(r.SchoolId, r.EnrollmentId, actor, ct);
        await using var s = await store.OpenAsync(r.SchoolId, false, ct);
        var (training, _, version, _) = await Programme(s, r.EnrollmentId, ct);
        var definition = await Required<AssessmentDefinitionVersion>(s, r.AssessmentDefinitionVersionId, ct);
        if (definition.ProgramVersionId != version.Id || r.StartsAt >= r.EndsAt || r.StartsAt < Now) throw LearningErrors.Rule("ASSESSMENT_NOT_APPLICABLE");
        var c = await Credit(s, training.Id, ct);
        // US-010 / AC-025: a repeated assessment is applicable only while this enrollment's assessment requirement is outstanding;
        // Scheduling then rejects the allocation (ASSESSMENT_NOT_READY) and writes nothing.
        var outstanding = !c.Results.Any(x => x.Attempt.DefinitionVersionId == definition.Id && x.Status == ResultStatus.Recorded && x.Version.Outcome == Outcome.Passed);
        var allowed = outstanding;
        if (definition.Stage == AssessmentStage.Final)
        {
            var evaluation = await Required<ReadinessEvaluation>(s, r.ReadinessEvaluationId, ct);
            var root = await Required<AssessmentReadiness>(s, evaluation.ReadinessId, ct);
            var decision = root.CurrentDecisionId is Guid id ? await Required<ReadinessDecision>(s, id, ct) : null;
            allowed = outstanding && evaluation.EnrollmentId == training.Id && evaluation.DefinitionVersionId == definition.Id && root.CurrentEvaluationId == evaluation.Id &&
                evaluation.DependencyRevision == training.DependencyRevision && decision?.Decision == Decision.Approved && decision.InvalidatedAt is null && c.SourcesVerified &&
                Requirements(c, await s.ListAsync<AdmissionRequirement>(x => x.DefinitionVersionId == definition.Id, ct)).All(x => x.Status == FormalStatus.Satisfied);
        }
        return new(r.SchoolId, training.Id, definition.Id, r.ReadinessEvaluationId, training.StudentId, definition.TrainingKind, training.CategoryCode, allowed, true, Now.AddSeconds(20));
    }
    public async Task<LearningPlan> PlanAsync(PlanningInputsRequest request, CancellationToken ct)
    {
        var x = request.Inputs; Id(x.SchoolId); Id(request.ActorUserId);
        if (x.ProgramVersionId is not Guid programVersion || x.TeacherIds is null || x.TeacherIds.Length == 0 || x.Windows is null || x.Windows.Length == 0 || x.PlanningStart is null || x.PlanningEnd is null ||
            x.PlanningEnd < x.PlanningStart || x.PlanningEnd.Value.DayNumber - x.PlanningStart.Value.DayNumber >= 90 || (x.EnrollmentIds?.Length ?? 0) + (x.GroupIds?.Length ?? 0) == 0 ||
            x.Windows.Length > 1000 || x.Windows.Any(w => w is null || w.StartsAt >= w.EndsAt) || (x.EnrollmentIds?.Length ?? 0) + (x.GroupIds?.Length ?? 0) > 100) throw LearningErrors.Invalid("PLANNING_INPUTS_INCOMPLETE");
        var actor = new LearningActor(Guid.Empty, request.ActorUserId, null, null, Now.AddSeconds(30), Guid.NewGuid().ToString("D"), Channel.Integration);
        var authority = await Authority(x.SchoolId, actor, null, ct); Admin(authority);
        var resources = new List<(Guid? Enrollment, Guid? Group, PlanningResources Resource)>();
        foreach (var enrollment in (x.EnrollmentIds ?? []).Distinct().Order()) resources.Add((enrollment, null, await owners.ResourcesAsync(x.SchoolId, actor.UserId, enrollment, null, x.TeacherIds[0], ct)));
        foreach (var group in (x.GroupIds ?? []).Distinct().Order()) resources.Add((null, group, await owners.ResourcesAsync(x.SchoolId, actor.UserId, null, group, x.TeacherIds[0], ct)));
        var enrollments = resources.SelectMany(r => r.Resource.Participants).Select(p => p.EnrollmentId).Distinct().Order().ToArray();
        foreach (var enrollment in enrollments) await RefreshSources(x.SchoolId, enrollment, actor, ct);
        var allocations = await owners.AllocationsAsync(x.SchoolId, x.EnrollmentIds ?? [], x.GroupIds ?? [], ct);
        if (allocations.SchoolId != x.SchoolId || allocations.Revision < 0 || allocations.Items is null) throw LearningErrors.Dependency("INVALID_ALLOCATION_BASIS");
        await using var s = await store.OpenAsync(x.SchoolId, false, ct);
        var version = await Required<TrainingProgramVersion>(s, programVersion, ct); var program = await Required<TrainingProgram>(s, version.ProgramId, ct);
        var planning = await s.ListAsync<LessonPlanningPolicy>(p => p.ProgramVersionId == programVersion, ct);
        var credits = new Dictionary<Guid, CreditState>();
        foreach (var enrollment in enrollments)
        {
            var c = await Credit(s, enrollment, ct);
            if (c.Training.ProgramVersionId != programVersion || !c.SourcesVerified) throw LearningErrors.Dependency("PROGRAMME_OR_PROGRESS_UNVERIFIED");
            credits.Add(enrollment, c);
        }
        var demands = new List<PlanningDemand>(); var basis = new List<string>();
        foreach (var resource in resources)
        {
            var r = resource.Resource;
            if (r.SchoolId != x.SchoolId || r.Category != program.CategoryCode || r.ValidUntil <= Now || r.BranchId == Guid.Empty || r.Participants.Length == 0) throw LearningErrors.Dependency("INVALID_RESOURCE_BASIS");
            foreach (var kind in resource.Enrollment is null ? new[] { TrainingKind.Theory } : new[] { TrainingKind.Practical })
            {
                var policy = planning.SingleOrDefault(p => p.Kind == kind) ?? throw LearningErrors.Rule("PLANNING_POLICY_REQUIRED");
                var residuals = r.Participants.Select(p =>
                {
                    var c = credits[p.EnrollmentId];
                    var alreadyAllocated = allocations.Items.Where(a => a.Kind == kind && a.Status is "Scheduled" or "Completed" && (kind == TrainingKind.Practical ? a.EnrollmentId == p.EnrollmentId : a.GroupId == resource.Group))
                        .Where(a => !c.Practical.Any(t => t.Record.LessonId == a.LessonId && t.Eligible) && !c.Theory.Any(t => t.Record.LessonId == a.LessonId && t.Eligible)).Sum(a => a.Minutes);
                    return Math.Max(0, (kind == TrainingKind.Theory ? version.RequiredTheoryMinutes - c.TheoryMinutes : version.RequiredPracticalMinutes - c.PracticalMinutes) - alreadyAllocated);
                }).Distinct().ToArray();
                // A group cannot silently credit or over-allocate learners with different remaining curricula.
                if (residuals.Length != 1) throw LearningErrors.Rule("GROUP_DEMAND_REQUIRES_EXPLICIT_SPLIT");
                var lengths = LearningRules.SplitDemand(residuals[0], policy);
                foreach (var length in lengths.GroupBy(n => n).OrderBy(g => g.Key))
                {
                    var demandId = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{programVersion:D}/{resource.Enrollment}/{resource.Group}/{kind}/{length.Key}"))[..16]);
                    var options = new List<LessonOption>();
                    foreach (var teacher in x.TeacherIds.Intersect(r.TeacherIds).Order())
                    foreach (var window in x.Windows.OrderBy(w => w.StartsAt))
                    {
                        for (var start = window.StartsAt; start.AddMinutes(length.Key) <= window.EndsAt; start = start.AddMinutes(length.Key))
                        {
                            var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")).DateTime);
                            if (localDate < x.PlanningStart || localDate > x.PlanningEnd) continue;
                            foreach (var vehicle in kind == TrainingKind.Practical ? (x.VehicleIds ?? []).Select(v => (Guid?)v) : new Guid?[] { null })
                            {
                                options.Add(new(x.SchoolId, r.BranchId, teacher, r.Category, start, start.AddMinutes(length.Key), resource.Enrollment,
                                    resource.Enrollment is null ? null : r.Participants.Single(p => p.EnrollmentId == resource.Enrollment).StudentId, vehicle, resource.Group));
                                if (options.Count + demands.Sum(d => d.Options.Length) > 1000) throw LearningErrors.Rule("PLANNING_LIMIT_EXCEEDED");
                            }
                        }
                    }
                    if (options.Count == 0) throw LearningErrors.Rule("NO_CONFIGURED_PLACEMENT_OPTIONS");
                    demands.Add(new(demandId, kind, length.Key, length.Count(), options.ToArray()));
                }
            }
            basis.Add(Wire.Save(new { resource.Enrollment, resource.Group, r.Category, r.BranchId, r.Participants, r.TeacherIds }));
        }
        if (demands.Sum(d => d.Count) > 100) throw LearningErrors.Rule("PLANNING_LIMIT_EXCEEDED");
        var basisVersion = Hash(Wire.Save(new { version.Id, version.Version, authority.SchoolRevision, allocations.Revision, allocations.Items,
            dependencies = credits.OrderBy(c => c.Key).Select(c => new { c.Key, c.Value.Training.DependencyRevision }), basis }));
        return new(x.SchoolId, programVersion, basisVersion, true, demands.ToArray());
    }
}
