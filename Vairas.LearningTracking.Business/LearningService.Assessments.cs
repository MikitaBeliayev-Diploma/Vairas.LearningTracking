using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private static void Mark(AssessmentDefinitionVersion definition, int mark)
    { if (mark < definition.MinimumMark || mark > definition.MaximumMark) throw LearningErrors.Rule("MARK_OUTSIDE_PINNED_SCALE"); }
    private static AssessmentResponse AssessmentDto(Assessment attempt, AssessmentResultVersion result, ReadinessDecision? decision) => new(attempt.Id, result.Id, attempt.EnrollmentId,
        attempt.DefinitionVersionId, attempt.AttemptNumber, attempt.Revision, result.Version, result.Mark, result.Outcome, LearningRules.HistoricalStatus(result, decision), result.AssessedAt, result.ReadinessDecisionId,
        decision?.InvalidatedAt, result.ReviewedInvalidationAt);
    private static async Task TopicMarks(ILearningSession s, AssessmentDefinitionVersion definition, AssessmentResultVersion version, TopicMarkInput[]? marks, CancellationToken ct)
    {
        if (marks is null) return;
        if (marks.Length > 500 || marks.Any(x => x is null) || marks.Select(x => x.AssessmentTopicId).Distinct().Count() != marks.Length) throw LearningErrors.Invalid();
        var topics = await s.ListAsync<AssessmentTopic>(x => x.DefinitionVersionId == definition.Id, ct);
        foreach (var mark in marks)
        {
            Mark(definition, mark.Mark);
            if (!topics.Any(x => x.Id == mark.AssessmentTopicId)) throw LearningErrors.Rule("ASSESSMENT_TOPIC_MISMATCH");
            s.Add(new AssessmentTopicResult { Id = Guid.NewGuid(), SchoolId = version.SchoolId, ResultVersionId = version.Id, DefinitionVersionId = definition.Id, AssessmentTopicId = mark.AssessmentTopicId, Mark = mark.Mark, Comment = mark.Comment });
        }
    }
    public async Task<CommandResult<AssessmentResponse>> AssessAsync(AssessmentResultRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.EnrollmentId); Id(request.AssessmentDefinitionVersionId);
        if (request.AssessedAt == default || request.AssessedAt > Now || request.Comment?.Length > 2000) throw LearningErrors.Invalid();
        var authority = await Authority(request.SchoolId, actor, request.EnrollmentId, ct); Assessor(authority);
        if (actor.TeacherId is null) throw LearningErrors.Denied();
        await RefreshSources(request.SchoolId, request.EnrollmentId, actor, ct);
        if (request.AppointmentId is not null) await VerifyAppointment(request, actor, ct);
        return await Command(request.SchoolId, operation, "RecordAssessment", request.EnrollmentId, request, actor, authority, async s =>
        {
            var (training, _, programme, _) = await Programme(s, request.EnrollmentId, ct);
            var definition = await Required<AssessmentDefinitionVersion>(s, request.AssessmentDefinitionVersionId, ct);
            if (definition.ProgramVersionId != programme.Id || request.AssessedAt < training.CreatedAt) throw LearningErrors.Rule("ASSESSMENT_PROGRAMME_MISMATCH");
            Mark(definition, request.Mark);
            ReadinessDecision? decision = null;
            if (definition.Stage == AssessmentStage.Final)
            {
                if (request.ReadinessDecisionId is not Guid decisionId) throw LearningErrors.Rule("CURRENT_READINESS_APPROVAL_REQUIRED");
                decision = await Required<ReadinessDecision>(s, decisionId, ct);
                var root = await Required<AssessmentReadiness>(s, decision.ReadinessId, ct);
                var evaluation = await Required<ReadinessEvaluation>(s, decision.EvaluationId, ct);
                var c = await Credit(s, request.EnrollmentId, ct);
                if (decision.EnrollmentId != training.Id || root.DefinitionVersionId != definition.Id || root.CurrentDecisionId != decision.Id || decision.Decision != Decision.Approved || decision.InvalidatedAt is not null ||
                    decision.DecidedAt > request.AssessedAt || evaluation.DependencyRevision != training.DependencyRevision || !c.SourcesVerified ||
                    Requirements(c, await s.ListAsync<AdmissionRequirement>(x => x.DefinitionVersionId == definition.Id, ct)).Any(x => x.Status != FormalStatus.Satisfied)) throw LearningErrors.Rule("CURRENT_READINESS_APPROVAL_REQUIRED");
            }
            else if (request.ReadinessDecisionId is not null) throw LearningErrors.Rule("MIDTERM_HAS_NO_FINAL_READINESS");
            var attempts = await s.ListAsync<Assessment>(x => x.EnrollmentId == training.Id && x.DefinitionVersionId == definition.Id, ct);
            var attempt = new Assessment { Id = Guid.NewGuid(), SchoolId = request.SchoolId, EnrollmentId = training.Id, ProgramVersionId = programme.Id, DefinitionVersionId = definition.Id,
                AttemptNumber = attempts.Count == 0 ? 1 : attempts.Max(x => x.AttemptNumber) + 1, AppointmentId = request.AppointmentId, Revision = 1 };
            if (request.AppointmentId is Guid appointment && await s.FindAsync<Assessment>(x => x.AppointmentId == appointment, ct) is not null) throw LearningErrors.Conflict("APPOINTMENT_ALREADY_RECORDED");
            s.Add(attempt);
            var result = new AssessmentResultVersion { Id = Guid.NewGuid(), SchoolId = request.SchoolId, AssessmentId = attempt.Id, EnrollmentId = training.Id, DefinitionVersionId = definition.Id,
                Version = 1, CommandId = operation, Status = ResultStatus.Recorded, AssessorId = actor.TeacherId.Value, AssessedAt = request.AssessedAt.ToUniversalTime(), RecordedBy = actor.UserId, RecordedAt = Now,
                Mark = request.Mark, Outcome = request.Mark >= definition.PassingMark ? Outcome.Passed : Outcome.Failed, ReadinessId = decision?.ReadinessId, ReadinessDecisionId = decision?.Id, Comment = request.Comment };
            s.Add(result); await TopicMarks(s, definition, result, request.TopicMarks, ct);
            await Touch(s, request.SchoolId, [training.Id], actor, "ASSESSMENT_RESULT_RECORDED", ct, InvalidationScope.ProspectiveOnly, preserveDecision: decision?.Id);
            Event(s, request.SchoolId, attempt.Id, attempt.Revision, result.Outcome == Outcome.Passed ? "AssessmentPassed" : "AssessmentFailed",
                new { schoolId = request.SchoolId, enrollmentId = training.Id, assessmentId = attempt.Id, assessedAt = result.AssessedAt }, actor);
            return AssessmentDto(attempt, result, decision);
        }, ct);
    }
    public async Task<CommandResult<AssessmentResponse>> CorrectAssessmentAsync(Guid school, Guid assessment, CorrectAssessmentRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Text(request.CorrectionReason); if (!Enum.IsDefined(request.Status)) throw LearningErrors.Invalid();
        Assessment attempt;
        await using (var read = await store.OpenAsync(school, false, ct)) attempt = await Required<Assessment>(read, assessment, ct);
        var authority = await Authority(school, actor, attempt.EnrollmentId, ct); Assessor(authority);
        await RefreshSources(school, attempt.EnrollmentId, actor, ct);
        return await Command(school, operation, "CorrectAssessment", assessment, request, actor, authority, async s =>
        {
            var root = await Required<Assessment>(s, assessment, ct);
            if (root.Revision != request.ExpectedRevision) throw LearningErrors.Conflict();
            var prior = (await s.ListAsync<AssessmentResultVersion>(x => x.AssessmentId == root.Id, ct)).MaxBy(x => x.Version)!;
            var definition = await Required<AssessmentDefinitionVersion>(s, root.DefinitionVersionId, ct); Mark(definition, request.Mark);
            var decision = prior.ReadinessDecisionId is Guid decisionId ? await Required<ReadinessDecision>(s, decisionId, ct) : null;
            if (prior.Status == ResultStatus.Annulled && request.Status != ResultStatus.Annulled) throw LearningErrors.Rule("ANNULLED_ATTEMPT_CLOSED");
            if (request.Status == ResultStatus.Annulled && request.Mark != prior.Mark) throw LearningErrors.Rule("ANNULMENT_RETAINS_HISTORICAL_MARK");
            if (LearningRules.HistoricalStatus(prior, decision) == ResultStatus.ReviewRequired || request.ReviewedInvalidationAt is not null || request.Status == ResultStatus.ReviewRequired)
            {
                if (decision?.InvalidatedAt is null || request.ReviewedInvalidationAt != decision.InvalidatedAt) throw LearningErrors.Rule("EXACT_HISTORICAL_REVIEW_REQUIRED");
                if (decision.InvalidatedAt <= prior.AssessedAt || decision.DecidedAt > prior.AssessedAt)
                { if (request.Status == ResultStatus.Recorded) throw LearningErrors.Rule("ASSESSMENT_WITHOUT_VALID_APPROVAL"); }
            }
            var version = new AssessmentResultVersion { Id = Guid.NewGuid(), SchoolId = school, AssessmentId = root.Id, EnrollmentId = root.EnrollmentId, DefinitionVersionId = definition.Id,
                Version = prior.Version + 1, CommandId = operation, Status = request.Status, AssessorId = prior.AssessorId, AssessedAt = prior.AssessedAt, RecordedBy = actor.UserId, RecordedAt = Now,
                Mark = request.Mark, Outcome = request.Mark >= definition.PassingMark ? Outcome.Passed : Outcome.Failed, ReadinessId = prior.ReadinessId, ReadinessDecisionId = prior.ReadinessDecisionId,
                Comment = request.Comment, CorrectionReason = request.CorrectionReason, ReviewedInvalidationAt = request.ReviewedInvalidationAt };
            s.Add(version); await TopicMarks(s, definition, version, request.TopicMarks, ct); root.Revision++;
            await Touch(s, school, [root.EnrollmentId], actor, "ASSESSMENT_RESULT_CORRECTED", ct);
            Event(s, school, root.Id, root.Revision, "AssessmentResultRevised", new { schoolId = school, enrollmentId = root.EnrollmentId, assessmentId = root.Id, version = version.Version, status = version.Status }, actor);
            return AssessmentDto(root, version, decision);
        }, ct);
    }
    public async Task<AssessmentResponse[]> AssessmentsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        await Authority(school, actor, enrollment, ct); await RefreshSources(school, enrollment, actor, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var attempts = await s.ListAsync<Assessment>(x => x.EnrollmentId == enrollment, ct);
        var versions = await s.ListAsync<AssessmentResultVersion>(x => x.EnrollmentId == enrollment, ct);
        var decisions = await s.ListAsync<ReadinessDecision>(x => x.EnrollmentId == enrollment, ct);
        return attempts.OrderBy(x => x.AttemptNumber).Select(a => { var r = versions.Where(x => x.AssessmentId == a.Id).MaxBy(x => x.Version)!; return AssessmentDto(a, r, decisions.SingleOrDefault(x => x.Id == r.ReadinessDecisionId)); }).ToArray();
    }
    private async Task VerifyAppointment(AssessmentResultRequest request, LearningActor actor, CancellationToken ct)
    {
        var appointment = await owners.AppointmentAsync(request.SchoolId, request.AppointmentId!.Value, ct);
        if (appointment.SchoolId != request.SchoolId || appointment.EnrollmentId != request.EnrollmentId || appointment.DefinitionVersionId != request.AssessmentDefinitionVersionId ||
            appointment.TeacherId != actor.TeacherId || appointment.Status == "Cancelled" || request.AssessedAt < appointment.StartsAt || request.AssessedAt > appointment.EndsAt)
            throw LearningErrors.Rule("ASSESSMENT_APPOINTMENT_MISMATCH");
    }
    public async Task<AssessmentVersionHistory[]> AssessmentHistoryAsync(Guid school, Guid assessment, LearningActor actor, CancellationToken ct)
    {
        Assessment attempt;
        await using (var read = await store.OpenAsync(school, false, ct)) attempt = await Required<Assessment>(read, assessment, ct);
        await Authority(school, actor, attempt.EnrollmentId, ct); await RefreshSources(school, attempt.EnrollmentId, actor, ct); Admission(actor);
        await using var s = await store.OpenAsync(school, false, ct);
        var versions = await s.ListAsync<AssessmentResultVersion>(x => x.AssessmentId == assessment, ct);
        var versionIds = versions.Select(x => x.Id).ToArray();
        var marks = await s.ListAsync<AssessmentTopicResult>(x => versionIds.Contains(x.ResultVersionId), ct);
        var decisions = await s.ListAsync<ReadinessDecision>(x => x.EnrollmentId == attempt.EnrollmentId, ct);
        var maximum = versions.Max(x => x.Version);
        return versions.OrderBy(x => x.Version).Select(v => new AssessmentVersionHistory(v.Id, v.Version, v.Version == maximum, v.Mark, v.Outcome, v.Status,
            LearningRules.HistoricalStatus(v, decisions.SingleOrDefault(d => d.Id == v.ReadinessDecisionId)), v.AssessedAt, v.RecordedAt, v.RecordedBy, v.CorrectionReason,
            v.ReviewedInvalidationAt, marks.Where(m => m.ResultVersionId == v.Id).Select(m => new TopicMarkInput(m.AssessmentTopicId, m.Mark, m.Comment)).ToArray())).ToArray();
    }
}
