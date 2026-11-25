using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService : ILearningDocuments
{
    private async Task<LearningAuthority> DocumentAuthority(Guid school, Guid enrollment, Guid actor, CancellationToken ct, bool admin = true, Guid? teacher = null)
    {
        Id(actor); var authority = await owners.AuthorityAsync(school,actor,enrollment,teacher,null,ct);
        if (authority.SchoolId != school || authority.ActorUserId != actor || authority.EnrollmentId != enrollment || authority.ValidUntil <= Now ||
            (admin ? !authority.Administrator : !authority.Administrator && !authority.Self && !authority.AssignedTeacher))
            throw LearningErrors.Denied(); return authority;
    }
    public async Task<DocumentEvidencePayload> DocumentEvidenceAsync(Guid school, Guid enrollment, Guid snapshotId, Guid actor, CancellationToken ct)
    {
        await DocumentAuthority(school,enrollment,actor,ct);
        await RefreshSources(school,enrollment,SystemActor(Guid.NewGuid().ToString("D")),ct);
        await using var s = await store.OpenAsync(school,false,ct);
        var snapshot = await Required<TrainingEvidenceSnapshot>(s,snapshotId,ct);
        if (snapshot.EnrollmentId != enrollment) throw LearningErrors.Missing();
        var credit = await Credit(s,enrollment,ct); var rows = new List<DocumentEvidenceRow>();
        foreach (var item in (await s.ListAsync<TrainingEvidenceItem>(x => x.SnapshotId == snapshotId && x.EnrollmentId == enrollment,ct)).OrderBy(x => x.Id))
        {
            if (item.PracticalRecordVersionId is Guid practical)
            {
                var v = await Required<PracticalRecordVersion>(s,practical,ct); var source = await Required<LessonSourceSnapshot>(s,v.SourceSnapshotId,ct);
                var links = await s.ListAsync<PracticalRecordTopic>(x => x.RecordVersionId == v.Id,ct); var topicIds = links.Select(x => x.TopicId).ToArray();
                var topics = (await s.ListAsync<TrainingTopic>(x => topicIds.Contains(x.Id),ct)).OrderBy(x => x.TopicKey).Select(x => x.TopicKey).ToArray();
                var confirmations = (await s.ListAsync<PracticalRecordResponse>(x => x.RecordVersionId == v.Id && x.Sequence <= item.RecordResponseSequence,ct)).OrderBy(x => x.Sequence)
                    .Select(x => $"{x.ActorRole}:{x.Response}:{x.ActorId:D}:{x.RespondedAt:O}").ToArray();
                rows.Add(new("Practical",item.Id,item.LessonId,item.ObservedState,item.CreditedMinutesAtSnapshot ?? 0,source.ActualStart,source.ActualEnd,v.DistanceKm,topics,confirmations,null,null,null,v.Id,source.Id));
            }
            else if (item.TheoryAttendanceId is Guid theory)
            {
                var attendance = await Required<TheoryAttendance>(s,theory,ct);
                if (attendance.EnrollmentId != enrollment) throw LearningErrors.Dependency("EVIDENCE_OWNER_MISMATCH");
                var v = await Required<TheoryRecordVersion>(s,attendance.RecordVersionId,ct); var source = await Required<LessonSourceSnapshot>(s,v.SourceSnapshotId,ct);
                rows.Add(new("Theory",item.Id,item.LessonId,item.ObservedState,item.CreditedMinutesAtSnapshot ?? 0,source.ActualStart,source.ActualEnd,null,[],
                    v.ConfirmedAt is not null && v.ConfirmedAt <= snapshot.PreparedAt ? [$"Teacher:Confirm:{v.ConfirmedBy}:{v.ConfirmedAt:O}"] : [],null,null,null,v.Id,source.Id));
            }
            else if (item.ResultVersionId is Guid result)
            { var v = await Required<AssessmentResultVersion>(s,result,ct); rows.Add(new("Assessment",item.Id,null,item.ObservedState,0,null,null,null,[],[],v.Outcome.ToString(),v.Mark,v.AssessedAt,v.Id)); }
        }
        var checks = (await s.ListAsync<TrainingEvidenceCheck>(x => x.SnapshotId == snapshotId,ct)).OrderBy(x => x.CheckCode).ThenBy(x => x.FieldCode)
            .Select(x => new DocumentEvidenceCheck(x.CheckCode,x.FieldCode,x.Outcome.ToString(),x.Explanation)).ToArray();
        var current = await CurrentEvidence(s,snapshot,ct);
        var rules = (await s.ListAsync<ProgramRuleSource>(x => x.ProgramVersionId == snapshot.ProgramVersionId,ct)).OrderBy(x => x.RuleCode)
            .Select(x => new DocumentEvidenceRuleSource(x.RuleCode,x.SourceUri,x.SourceEdition,x.Applicability,x.VerifiedBy,x.VerifiedAt)).ToArray();
        return new(school,enrollment,snapshotId,snapshot.Kind == EvidenceKind.Journal ? "PracticalJournal" : "CompletionReport",snapshot.DependencyRevision,credit.Training.DependencyRevision,
            snapshot.Status.ToString(),current.Current,credit.SourcesVerified,$"evidence-{snapshot.Id:D}-{snapshot.DependencyRevision}","learning-document-evidence-v1",snapshot.PreparedAt,
            rows.OrderBy(x => x.ActualStart ?? x.AssessedAt ?? DateTimeOffset.MaxValue).ThenBy(x => x.ItemId).ToArray(),checks,snapshot.ProgramVersionId,snapshot.ChecklistVersionId,rules);
    }
    public async Task<DocumentReleaseResponse> DecideDocumentAsync(Guid operation, DocumentReleaseRequest request, CancellationToken ct)
    {
        Id(operation); Id(request.DocumentVersionId); Id(request.SnapshotId);
        if (request.Purpose is not ("Publication" or "CurrentUse") || request.Kind is not ("PracticalJournal" or "CompletionReport") ||
            request.ArtifactDigest.Length != 64 || request.ArtifactDigest.Any(c => !Uri.IsHexDigit(c))) throw LearningErrors.Invalid();
        var authority = await DocumentAuthority(request.SchoolId,request.EnrollmentId,request.ActorUserId,ct,request.Purpose == "Publication",request.TeacherId);
        await RefreshSources(request.SchoolId,request.EnrollmentId,SystemActor(operation.ToString("D")),ct);
        var fingerprint = Hash(Wire.Save(request));
        // OpenAsync(write:true) uses the same school advisory lock as corrections and dependency changes.
        await using var s = await store.OpenAsync(request.SchoolId,true,ct);
        if (authority.ValidUntil <= Now) throw LearningErrors.Dependency("AUTHORITY_EXPIRED");
        var prior = await s.FindAsync<DocumentReleaseDecision>(x => x.Id == operation,ct);
        if (prior is not null)
        { if (prior.Fingerprint != fingerprint || prior.ActorId != request.ActorUserId) throw LearningErrors.Conflict("IDEMPOTENCY_KEY_REUSED"); return ReleaseDto(prior); }
        var snapshot = await Required<TrainingEvidenceSnapshot>(s,request.SnapshotId,ct);
        if (snapshot.EnrollmentId != request.EnrollmentId) throw LearningErrors.Missing();
        var current = await CurrentEvidence(s,snapshot,ct); var training = await Required<StudentTraining>(s,request.EnrollmentId,ct);
        var accepted = snapshot.Status == EvidenceStatus.Ready && current.Current && training.DependencyRevision == request.ExpectedDependencyRevision &&
            snapshot.DependencyRevision == request.ExpectedDependencyRevision && (snapshot.Kind == EvidenceKind.Journal ? "PracticalJournal" : "CompletionReport") == request.Kind &&
            (request.Kind != "CompletionReport" || authority.EnrollmentStatus is "Active" or "Completed");
        var d = new DocumentReleaseDecision { Id = operation, SchoolId = request.SchoolId, EnrollmentId = request.EnrollmentId, ActorId = request.ActorUserId,
            Fingerprint = fingerprint, Status = accepted ? "Accepted" : "Rejected", EvaluatedRevision = training.DependencyRevision, DecidedAt = Now,
            Reason = accepted ? "Ready at this owner decision instant." : "Exact current Ready evidence is required." };
        s.Add(d); await s.CommitAsync(ct); return ReleaseDto(d);
    }
    public async Task<DocumentReleaseResponse?> DocumentDecisionAsync(Guid school, Guid operation, Guid actor, CancellationToken ct)
    {
        DocumentReleaseDecision? d; await using (var s = await store.OpenAsync(school,false,ct)) d = await s.FindAsync<DocumentReleaseDecision>(x => x.Id == operation && x.ActorId == actor,ct);
        if (d is null) return null; await DocumentAuthority(school,d.EnrollmentId,actor,ct); return ReleaseDto(d);
    }
    private static DocumentReleaseResponse ReleaseDto(DocumentReleaseDecision d) => new(d.Id,d.Status,d.EvaluatedRevision,d.DecidedAt,d.Reason);
    public async Task<long> DocumentRevisionAsync(Guid school, Guid enrollment, CancellationToken ct)
    {
        await InternalEnrollment(school,enrollment,ct);
        if (!await RefreshSources(school,enrollment,SystemActor(Guid.NewGuid().ToString("D")),ct)) throw LearningErrors.Dependency("SOURCE_UNAVAILABLE");
        await using var s = await store.OpenAsync(school,false,ct); return (await Required<StudentTraining>(s,enrollment,ct)).DependencyRevision;
    }
}
