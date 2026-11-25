using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private async Task<CreditState> Credit(ILearningSession s, Guid enrollment, CancellationToken ct)
    {
        var (training, _, programme, policy) = await Programme(s, enrollment, ct, false);
        var applicable = policy is not null && programme.LegalBasisVerified && programme.Status != ProgramStatus.Draft && programme.EffectiveFrom <= Today && !(programme.EffectiveTo < Today);
        var practical = await s.ListAsync<PracticalTrainingRecord>(x => x.EnrollmentId == enrollment, ct);
        var practicalIds = practical.Select(x => x.Id).ToArray();
        var versions = await s.ListAsync<PracticalRecordVersion>(x => practicalIds.Contains(x.RecordId), ct);
        var responses = await s.ListAsync<PracticalRecordResponse>(x => practicalIds.Contains(x.RecordId), ct);
        var attendance = await s.ListAsync<TheoryAttendance>(x => x.EnrollmentId == enrollment, ct);
        var theoryIds = attendance.Select(x => x.RecordVersionId).ToArray();
        var theoryVersions = await s.ListAsync<TheoryRecordVersion>(x => theoryIds.Contains(x.Id), ct);
        var theoryRecordIds = theoryVersions.Select(x => x.RecordId).ToArray();
        var theoryRecords = await s.ListAsync<TheoryTrainingRecord>(x => theoryRecordIds.Contains(x.Id), ct);
        var lessons = practical.Select(x => x.LessonId).Concat(theoryVersions.Select(x => x.LessonId)).Distinct().ToArray();
        var sources = await s.ListAsync<LessonSourceSnapshot>(x => lessons.Contains(x.LessonId), ct);
        var states = await s.ListAsync<LessonSourceState>(x => lessons.Contains(x.LessonId), ct);
        var p = practical.Select(record =>
        {
            var version = versions.Single(x => x.Id == record.CurrentVersionId);
            var source = sources.Single(x => x.Id == version.SourceSnapshotId);
            var state = states.SingleOrDefault(x => x.LessonId == record.LessonId);
            var rs = responses.Where(x => x.RecordVersionId == version.Id).ToArray();
            return new PracticalCredit(record, version, rs, applicable && state is not null && LearningRules.PracticalEligible(record, version, source, state, rs, training));
        }).ToArray();
        var t = attendance.Where(row => theoryRecords.Any(record => record.CurrentVersionId == row.RecordVersionId)).Select(row =>
        {
            var version = theoryVersions.Single(x => x.Id == row.RecordVersionId);
            var record = theoryRecords.Single(x => x.Id == version.RecordId);
            var source = sources.Single(x => x.Id == version.SourceSnapshotId);
            var state = states.SingleOrDefault(x => x.LessonId == record.LessonId);
            return new TheoryCredit(record, version, row, applicable && state is not null && LearningRules.TheoryEligible(record, version, row, source, state, training));
        }).ToArray();
        var attempts = await s.ListAsync<Assessment>(x => x.EnrollmentId == enrollment, ct);
        var results = await s.ListAsync<AssessmentResultVersion>(x => x.EnrollmentId == enrollment, ct);
        var decisions = await s.ListAsync<ReadinessDecision>(x => x.EnrollmentId == enrollment, ct);
        var currentResults = attempts.Select(attempt =>
        {
            var result = results.Where(x => x.AssessmentId == attempt.Id).MaxBy(x => x.Version)!;
            return new ResultCredit(attempt, result, LearningRules.HistoricalStatus(result, decisions.SingleOrDefault(x => x.Id == result.ReadinessDecisionId)));
        }).ToArray();
        return new(training, programme, policy, p, t, currentResults,
            t.Where(x => x.Eligible).DistinctBy(x => x.Record.LessonId).Sum(x => x.Version.TrainingMinutes),
            p.Where(x => x.Eligible).DistinctBy(x => x.Record.LessonId).Sum(x => x.Version.TrainingMinutes ?? 0),
            applicable && lessons.All(id => states.Any(x => x.LessonId == id && x.Freshness == Freshness.Current)));
    }
    private static ProgressResponse ProgressDto(CreditState c)
    {
        var theoryDeficit = Math.Max(0, c.Version.RequiredTheoryMinutes - c.TheoryMinutes);
        var practicalDeficit = Math.Max(0, c.Version.RequiredPracticalMinutes - c.PracticalMinutes);
        var outstanding = new List<string>();
        if (!c.SourcesVerified) outstanding.Add("Source or applicable policy verification is unavailable.");
        if (theoryDeficit > 0) outstanding.Add($"{theoryDeficit} theory minutes remain.");
        if (practicalDeficit > 0) outstanding.Add($"{practicalDeficit} practical minutes remain.");
        var disputes = c.Practical.Count(x => x.Responses.Any(r => r.Response == ResponseKind.Dispute));
        var pending = c.Practical.Count(x => !x.Eligible) + c.Theory.Count(x => !x.Eligible && x.Attendance.Attendance == Attendance.Present);
        return new(c.Training.Id, c.TheoryMinutes, c.PracticalMinutes, LearningRules.Percent(c.TheoryMinutes, c.PracticalMinutes, c.Version.RequiredTheoryMinutes, c.Version.RequiredPracticalMinutes),
            outstanding.ToArray(), pending, disputes, theoryDeficit, practicalDeficit, c.Training.DependencyRevision, c.SourcesVerified);
    }
    public async Task<ProgressResponse> ProgressAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        await Authority(school, actor, enrollment, ct);
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            var training = await read.FindAsync<StudentTraining>(x => x.Id == enrollment, ct);
            if (training?.ProgramVersionId is null) return new(enrollment, 0, 0, null, ["An applicable programme has not been assigned."], 0, 0, null, null, training?.DependencyRevision ?? 0, false, "AwaitingProgramme");
        }
        await RefreshSources(school, enrollment, actor, ct);
        Admission(actor);
        await using var s = await store.OpenAsync(school, false, ct); return ProgressDto(await Credit(s, enrollment, ct));
    }
    private static RequirementResult[] Requirements(CreditState c, IReadOnlyList<AdmissionRequirement> requirements)
    {
        return requirements.OrderBy(x => x.Code).Select(r =>
        {
            int? minutes = null, mark = null; bool satisfied;
            if (r.Kind is RequirementKind.MinimumTheoryMinutes or RequirementKind.MinimumPracticalMinutes)
            {
                minutes = r.Kind == RequirementKind.MinimumTheoryMinutes ? c.TheoryMinutes : c.PracticalMinutes;
                satisfied = minutes >= r.MinimumMinutes;
            }
            else
            {
                var results = c.Results.Where(x => x.Attempt.DefinitionVersionId == r.PrerequisiteDefinitionVersionId && x.Status == ResultStatus.Recorded).ToArray();
                mark = results.Length == 0 ? null : results.Max(x => x.Version.Mark);
                satisfied = r.Kind == RequirementKind.AssessmentPassed ? results.Any(x => x.Version.Outcome == Outcome.Passed) : mark >= r.MinimumMark;
            }
            var status = !c.SourcesVerified ? FormalStatus.Unverified : satisfied ? FormalStatus.Satisfied : FormalStatus.Incomplete;
            return new RequirementResult(r.Id, status, minutes, mark, status == FormalStatus.Satisfied ? "Requirement satisfied by current eligible evidence." : status == FormalStatus.Unverified ? "Authoritative basis is unverified." : "Requirement is outstanding.");
        }).ToArray();
    }
    public async Task<CommandResult<ReadinessResponse>> EvaluateAsync(Guid school, Guid enrollment, EvaluateReadinessRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.AssessmentDefinitionVersionId);
        var authority = await Authority(school, actor, enrollment, ct); Active(authority);
        await RefreshSources(school, enrollment, actor, ct);
        return await Command(school, operation, "EvaluateReadiness", enrollment, request, actor, authority, async s =>
        {
            var c = await Credit(s, enrollment, ct);
            var definition = await Required<AssessmentDefinitionVersion>(s, request.AssessmentDefinitionVersionId, ct);
            if (definition.ProgramVersionId != c.Training.ProgramVersionId || definition.Stage != AssessmentStage.Final) throw LearningErrors.Rule("FINAL_DEFINITION_REQUIRED");
            var requirements = await s.ListAsync<AdmissionRequirement>(x => x.DefinitionVersionId == definition.Id, ct);
            var results = Requirements(c, requirements);
            var root = await s.FindAsync<AssessmentReadiness>(x => x.EnrollmentId == enrollment && x.DefinitionVersionId == definition.Id, ct);
            if (root is null)
            {
                root = new AssessmentReadiness { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = enrollment, ProgramVersionId = definition.ProgramVersionId, DefinitionVersionId = definition.Id }; s.Add(root);
            }
            var evaluation = new ReadinessEvaluation { Id = Guid.NewGuid(), SchoolId = school, ReadinessId = root.Id, EnrollmentId = enrollment, DefinitionVersionId = definition.Id,
                DependencyRevision = c.Training.DependencyRevision, FormalStatus = !c.SourcesVerified || results.Any(x => x.Status == FormalStatus.Unverified) ? FormalStatus.Unverified : results.All(x => x.Status == FormalStatus.Satisfied) ? FormalStatus.Satisfied : FormalStatus.Incomplete,
                EvaluatedAt = Now, EvaluatedBy = actor.UserId, Explanation = "Current pre-assessment prerequisites; future assessment duration and outcome are excluded." };
            s.Add(evaluation);
            foreach (var r in results) s.Add(new ReadinessRequirementResult { Id = Guid.NewGuid(), SchoolId = school, EvaluationId = evaluation.Id, DefinitionVersionId = definition.Id,
                RequirementId = r.RequirementId, Status = r.Status, ObservedMinutes = r.ObservedMinutes, ObservedMark = r.ObservedMark, Explanation = r.Explanation });
            foreach (var p in c.Practical.Where(x => x.Eligible)) s.Add(new ReadinessEvidenceBasis { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = enrollment, EvaluationId = evaluation.Id,
                LessonId = p.Record.LessonId, PracticalRecordVersionId = p.Version.Id, ObservedMinutes = p.Version.TrainingMinutes });
            foreach (var t in c.Theory.Where(x => x.Eligible)) s.Add(new ReadinessEvidenceBasis { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = enrollment, EvaluationId = evaluation.Id,
                LessonId = t.Record.LessonId, TheoryAttendanceId = t.Attendance.Id, ObservedMinutes = t.Version.TrainingMinutes });
            foreach (var r in c.Results.Where(x => x.Status == ResultStatus.Recorded && requirements.Any(q => q.PrerequisiteDefinitionVersionId == x.Attempt.DefinitionVersionId)))
                s.Add(new ReadinessEvidenceBasis { Id = Guid.NewGuid(), SchoolId = school, EnrollmentId = enrollment, EvaluationId = evaluation.Id, ResultVersionId = r.Version.Id });
            root.CurrentEvaluationId = evaluation.Id; root.CurrentDecisionId = null; root.Revision++;
            await s.FlushAsync(ct); return await ReadinessDto(s, root, ct);
        }, ct);
    }
    private static async Task<ReadinessResponse> ReadinessDto(ILearningSession s, AssessmentReadiness root, CancellationToken ct)
    {
        var evaluation = await Required<ReadinessEvaluation>(s, root.CurrentEvaluationId!.Value, ct);
        var requirements = await s.ListAsync<ReadinessRequirementResult>(x => x.EvaluationId == evaluation.Id, ct);
        var decision = root.CurrentDecisionId is Guid id ? await Required<ReadinessDecision>(s, id, ct) : null;
        var training = await Required<StudentTraining>(s, root.EnrollmentId, ct);
        var stale = training.DependencyRevision != evaluation.DependencyRevision || decision?.InvalidatedAt is not null;
        var status = stale ? "ReevaluationRequired" : evaluation.FormalStatus != FormalStatus.Satisfied ? "RequirementsIncomplete" : decision is null ? "AwaitingAssessor" : decision.Decision.ToString();
        return new(evaluation.Id, root.Id, root.EnrollmentId, root.DefinitionVersionId, root.Revision, evaluation.FormalStatus, status,
            requirements.OrderBy(x => x.RequirementId).Select(x => new RequirementResult(x.RequirementId, x.Status, x.ObservedMinutes, x.ObservedMark, x.Explanation)).ToArray(), decision?.Id, decision?.Decision, stale,
            decision?.InvalidatedAt);
    }
    public async Task<CommandResult<ReadinessResponse>> DecideAsync(Guid school, Guid enrollment, ReadinessDecisionRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.EvaluationId); Text(request.Reason); if (!Enum.IsDefined(request.Decision)) throw LearningErrors.Invalid();
        var authority = await Authority(school, actor, enrollment, ct); Assessor(authority);
        await RefreshSources(school, enrollment, actor, ct);
        return await Command(school, operation, "DecideReadiness", enrollment, request, actor, authority, async s =>
        {
            var evaluation = await Required<ReadinessEvaluation>(s, request.EvaluationId, ct);
            if (evaluation.EnrollmentId != enrollment) throw LearningErrors.Missing();
            var root = await Required<AssessmentReadiness>(s, evaluation.ReadinessId, ct);
            if (root.Revision != request.ExpectedRevision || root.CurrentEvaluationId != evaluation.Id || root.CurrentDecisionId is not null) throw LearningErrors.Conflict("READINESS_EVALUATION_STALE");
            var c = await Credit(s, enrollment, ct);
            if (request.Decision == Decision.Approved)
            {
                var current = Requirements(c, await s.ListAsync<AdmissionRequirement>(x => x.DefinitionVersionId == evaluation.DefinitionVersionId, ct));
                if (evaluation.DependencyRevision != c.Training.DependencyRevision || !c.SourcesVerified || evaluation.FormalStatus != FormalStatus.Satisfied || current.Any(x => x.Status != FormalStatus.Satisfied))
                    throw LearningErrors.Rule("READINESS_REQUIREMENTS_NOT_SATISFIED");
            }
            var decision = new ReadinessDecision { Id = Guid.NewGuid(), SchoolId = school, ReadinessId = root.Id, EvaluationId = evaluation.Id, EnrollmentId = enrollment,
                ActorId = actor.UserId, DecidedAt = Now, Decision = request.Decision, Reason = request.Reason, AuthorityBasis = authority.AuthorityBasis, CommandId = operation };
            s.Add(decision); root.CurrentDecisionId = decision.Id; root.Revision++;
            await s.FlushAsync(ct); return await ReadinessDto(s, root, ct);
        }, ct);
    }
    public async Task<ReadinessResponse[]> ReadinessAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        await Authority(school, actor, enrollment, ct); await RefreshSources(school, enrollment, actor, ct);
        await using var s = await store.OpenAsync(school, false, ct); var result = new List<ReadinessResponse>();
        foreach (var root in await s.ListAsync<AssessmentReadiness>(x => x.EnrollmentId == enrollment, ct)) result.Add(await ReadinessDto(s, root, ct));
        return result.ToArray();
    }
}
