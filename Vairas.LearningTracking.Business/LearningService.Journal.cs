using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    public async Task<PracticalRecordResult[]> PracticalRecordsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        await Authority(school, actor, enrollment, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var rows = await s.ListAsync<PracticalTrainingRecord>(x => x.EnrollmentId == enrollment, ct);
        var result = new List<PracticalRecordResult>();
        foreach (var row in rows.OrderBy(x => x.Id)) result.Add(await PracticalDto(s, row, ct));
        return result.ToArray();
    }
    public async Task<CommandResult<PracticalRecordResult>> PracticalAsync(Guid? id, PracticalFactsRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.EnrollmentId); Id(request.LessonId); Id(request.ProgramVersionId); Id(request.SourceSnapshotId);
        if (request.TopicIds is null || request.TopicIds.Length == 0 || request.TopicIds.Length > 500 || request.TopicIds.Any(x => x == Guid.Empty) || request.TopicIds.Distinct().Count() != request.TopicIds.Length || request.DistanceKm < 0 || request.DistanceKm > 9999999.999m || decimal.Round(request.DistanceKm, 3) != request.DistanceKm) throw LearningErrors.Invalid();
        if (id is not null) { Text(request.CorrectionReason); if (request.ExpectedRevision is null) throw LearningErrors.Invalid(); }
        else if (request.ExpectedRevision is not null || request.CorrectionReason is not null) throw LearningErrors.Invalid("CREATE_HAS_NO_EXPECTED_REVISION");
        request = request with { TopicIds = request.TopicIds.Order().ToArray() };
        var authority = await Authority(request.SchoolId, actor, request.EnrollmentId, ct); Active(authority);
        var source = await FetchSource(request.SchoolId, request.LessonId, actor, ct); Teacher(actor, authority, source);
        return await Command(request.SchoolId, operation, id is null ? "CreatePracticalRecord" : "RevisePracticalRecord", id, request, actor, authority, async s =>
        {
            var currentSource = await ExactSource(s, request.SourceSnapshotId, request.LessonId, ct);
            Teacher(actor, authority, currentSource);
            if (currentSource.Kind != TrainingKind.Practical || currentSource.EnrollmentId != request.EnrollmentId) throw LearningErrors.Rule("SOURCE_ENROLLMENT_MISMATCH");
            var (training, _, version, policy) = await Programme(s, request.EnrollmentId, ct);
            if (training.StudentId != currentSource.StudentId || version.Id != request.ProgramVersionId) throw LearningErrors.Rule("PROGRAMME_BINDING_MISMATCH");
            LearningRules.TeachingMinutes(currentSource, request.TrainingMinutes, request.DurationExplanation);
            var topics = await s.ListAsync<TrainingTopic>(x => x.ProgramVersionId == version.Id, ct);
            if (request.TopicIds.Any(x => !topics.Any(t => t.Id == x))) throw LearningErrors.Rule("TOPIC_PROGRAMME_MISMATCH");
            if (policy is null) throw LearningErrors.Rule("VERIFIED_CHECKLIST_REQUIRED");
            PracticalTrainingRecord record;
            if (id is Guid existing)
            {
                record = await Required<PracticalTrainingRecord>(s, existing, ct);
                if (record.EnrollmentId != request.EnrollmentId || record.LessonId != request.LessonId) throw LearningErrors.Rule("RECORD_IDENTITY_IMMUTABLE");
                if (record.Revision != request.ExpectedRevision) throw LearningErrors.Conflict();
            }
            else
            {
                if (await s.FindAsync<PracticalTrainingRecord>(x => x.LessonId == request.LessonId && x.EnrollmentId == request.EnrollmentId, ct) is not null) throw LearningErrors.Conflict("PRACTICAL_RECORD_EXISTS");
                record = new PracticalTrainingRecord { Id = Guid.NewGuid(), SchoolId = request.SchoolId, LessonId = request.LessonId, EnrollmentId = request.EnrollmentId }; s.Add(record);
            }
            var old = record.CurrentVersionId is Guid previous ? await Required<PracticalRecordVersion>(s, previous, ct) : null;
            var fact = new PracticalRecordVersion { Id = Guid.NewGuid(), SchoolId = request.SchoolId, RecordId = record.Id, EnrollmentId = training.Id, LessonId = request.LessonId, ProgramVersionId = version.Id,
                Version = (old?.Version ?? 0) + 1, SourceSnapshotId = currentSource.Id, TrainingMinutes = request.TrainingMinutes, DurationExplanation = request.DurationExplanation,
                DistanceKm = request.DistanceKm, Validation = Validation.Valid, CreatedBy = actor.UserId, CreatedAt = Now, CorrectionReason = request.CorrectionReason };
            s.Add(fact);
            foreach (var topic in request.TopicIds) s.Add(new PracticalRecordTopic { Id = Guid.NewGuid(), SchoolId = request.SchoolId, RecordVersionId = fact.Id, ProgramVersionId = version.Id, TopicId = topic });
            var previousRevision = record.Revision;
            record.CurrentVersionId = fact.Id; record.Revision++;
            await Touch(s, request.SchoolId, [training.Id], actor, "PRACTICAL_FACT_VERSION_CHANGED", ct,
                old is null ? InvalidationScope.ProspectiveOnly : InvalidationScope.HistoricalReviewRequired);
            if (old is not null) Event(s, request.SchoolId, record.Id, record.Revision, "PracticalRecordRevised", new { schoolId = request.SchoolId, enrollmentId = training.Id,
                lessonId = record.LessonId, recordId = record.Id, previousRevision, recordRevision = record.Revision, revisedAt = Now }, actor);
            await s.FlushAsync(ct); return await PracticalDto(s, record, ct);
        }, ct);
    }
    private async Task<PracticalRecordResult> PracticalDto(ILearningSession s, PracticalTrainingRecord record, CancellationToken ct)
    {
        var fact = await Required<PracticalRecordVersion>(s, record.CurrentVersionId ?? throw LearningErrors.Missing(), ct);
        var topics = await s.ListAsync<PracticalRecordTopic>(x => x.RecordVersionId == fact.Id, ct);
        var responses = await s.ListAsync<PracticalRecordResponse>(x => x.RecordVersionId == fact.Id, ct);
        var state = responses.Any(x => x.Response == ResponseKind.Dispute) ? "Disputed" : responses.Where(x => x.Response == ResponseKind.Confirm).Select(x => x.ActorRole).Distinct().Count() == 2 ? "Confirmed" : "Pending";
        var credit = (await Credit(s, record.EnrollmentId, ct)).Practical.Single(x => x.Record.Id == record.Id).Eligible;
        if (state == "Confirmed" && !credit) state = "RevalidationRequired";
        return new(record.Id, record.EnrollmentId, record.Revision, fact.Version, state,
            new(fact.SchoolId, fact.EnrollmentId, fact.LessonId, fact.ProgramVersionId, fact.SourceSnapshotId, fact.TrainingMinutes ?? 0, fact.DistanceKm ?? 0, topics.Select(x => x.TopicId).Order().ToArray(), fact.DurationExplanation),
            responses.OrderBy(x => x.Sequence).Select(x => new ParticipantResponse(x.ActorRole, x.Response, fact.Version)).ToArray(), credit);
    }
    public async Task<PracticalRecordResult> PracticalReadAsync(Guid school, Guid id, LearningActor actor, CancellationToken ct)
    {
        PracticalTrainingRecord record;
        await using (var read = await store.OpenAsync(school, false, ct)) record = await Required<PracticalTrainingRecord>(read, id, ct);
        await Authority(school, actor, record.EnrollmentId, ct);
        await using var s = await store.OpenAsync(school, false, ct); return await PracticalDto(s, await Required<PracticalTrainingRecord>(s, id, ct), ct);
    }
    public async Task<CommandResult<PracticalRecordResult>> RespondAsync(Guid school, Guid id, long revision, ResponseKind response, string? reason, Guid operation, LearningActor actor, CancellationToken ct)
    {
        if (revision < 1 || !Enum.IsDefined(response)) throw LearningErrors.Invalid();
        if (response == ResponseKind.Dispute) Text(reason);
        PracticalTrainingRecord record;
        await using (var read = await store.OpenAsync(school, false, ct)) record = await Required<PracticalTrainingRecord>(read, id, ct);
        var authority = await Authority(school, actor, record.EnrollmentId, ct); Active(authority);
        await FetchSource(school, record.LessonId, actor, ct);
        return await Command(school, operation, response == ResponseKind.Confirm ? "ConfirmPracticalRecord" : "DisputePracticalRecord", id, new { revision, reason }, actor, authority, async s =>
        {
            var root = await Required<PracticalTrainingRecord>(s, id, ct);
            if (root.Revision != revision) throw LearningErrors.Conflict();
            var fact = await Required<PracticalRecordVersion>(s, root.CurrentVersionId!.Value, ct);
            var source = await ExactSource(s, fact.SourceSnapshotId, root.LessonId, ct);
            var (training, _, _, _) = await Programme(s, root.EnrollmentId, ct);
            ResponseRole role;
            if (actor.TeacherId == source.InstructorId && authority.Teacher) role = ResponseRole.Instructor;
            else if (actor.StudentId == training.StudentId && authority.Self) role = ResponseRole.Student;
            else throw LearningErrors.Denied();
            if (fact.Validation != Validation.Valid) throw LearningErrors.Rule("VALID_FACT_VERSION_REQUIRED");
            LearningRules.TeachingMinutes(source, fact.TrainingMinutes ?? 0, fact.DurationExplanation);
            var prior = await s.ListAsync<PracticalRecordResponse>(x => x.RecordId == root.Id, ct);
            var versionResponses = prior.Where(x => x.RecordVersionId == fact.Id).ToArray();
            if (versionResponses.Any(x => x.Response == ResponseKind.Dispute)) throw LearningErrors.Conflict("DISPUTED_VERSION_CLOSED");
            if (response == ResponseKind.Confirm && versionResponses.Any(x => x.ActorRole == role && x.Response == ResponseKind.Confirm)) return await PracticalDto(s, root, ct);
            s.Add(new PracticalRecordResponse { Id = Guid.NewGuid(), SchoolId = school, RecordId = root.Id, RecordVersionId = fact.Id,
                Sequence = prior.Count == 0 ? 1 : prior.Max(x => x.Sequence) + 1, ActorId = actor.UserId, ActorRole = role, Response = response, RespondedAt = Now, Reason = reason, CommandId = operation });
            root.Revision++;
            await Touch(s, school, [root.EnrollmentId], actor, response == ResponseKind.Dispute ? "PRACTICAL_VERSION_DISPUTED" : "PRACTICAL_CONFIRMATION_CHANGED", ct,
                response == ResponseKind.Dispute ? InvalidationScope.HistoricalReviewRequired : InvalidationScope.ProspectiveOnly);
            await s.FlushAsync(ct);
            var result = await PracticalDto(s, root, ct);
            if (result.Status == "Confirmed") Event(s, school, root.Id, root.Revision, "PracticalRecordConfirmed", new { schoolId = school, enrollmentId = root.EnrollmentId, lessonId = root.LessonId,
                recordId = root.Id, recordRevision = root.Revision, creditedDurationMinutes = fact.TrainingMinutes!.Value, confirmedAt = Now }, actor);
            else if (response == ResponseKind.Dispute) Event(s, school, root.Id, root.Revision, "PracticalRecordRevised", new { schoolId = school, enrollmentId = root.EnrollmentId, lessonId = root.LessonId,
                recordId = root.Id, previousRevision = revision, recordRevision = root.Revision, revisedAt = Now }, actor);
            return result;
        }, ct);
    }
    public async Task<CommandResult<TheoryRecordResult>> TheoryAsync(Guid? id, TheoryFactsRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.LessonId); Id(request.SourceSnapshotId);
        if (request.AttendanceRows is null || request.AttendanceRows.Length is 0 or > 1000 || request.AttendanceRows.Any(x => x is null)) throw LearningErrors.Invalid();
        if (id is not null) { Text(request.CorrectionReason); if (request.ExpectedRevision is null) throw LearningErrors.Invalid(); }
        else if (request.ExpectedRevision is not null || request.CorrectionReason is not null) throw LearningErrors.Invalid();
        request = request with { AttendanceRows = request.AttendanceRows.OrderBy(x => x.EnrollmentId).ToArray() };
        var incoming = await owners.SourceAsync(request.SchoolId, request.LessonId, ct);
        var authority = await Authority(request.SchoolId, actor, null, ct, incoming.TeacherId, incoming.GroupId);
        Teacher(actor, authority, SourceRow(incoming));
        var source = await Reconcile(request.SchoolId, request.LessonId, incoming, actor, ct);
        if (source.Kind != TrainingKind.Theory) throw LearningErrors.Rule("THEORY_SOURCE_REQUIRED");
        LearningRules.TeachingMinutes(source, request.TrainingMinutes, request.DurationExplanation);
        var roster = await owners.RosterAsync(request.SchoolId, source.GroupId!.Value, source.MembershipRevision!.Value, source.ActualStart!.Value, source.ActualEnd!.Value, ct);
        SameRoster(request.AttendanceRows, roster, source);
        return await Command(request.SchoolId, operation, id is null ? "CreateTheoryRecord" : "ReviseTheoryRecord", id, request, actor, authority, async s =>
        {
            var verified = await ExactSource(s, request.SourceSnapshotId, request.LessonId, ct);
            if (!LearningRules.SameSource(verified, source)) throw LearningErrors.Conflict("SOURCE_STALE");
            foreach (var member in request.AttendanceRows)
            {
                var (training, _, _, _) = await Programme(s, member.EnrollmentId, ct);
                if (training.StudentId != member.StudentId) throw LearningErrors.Rule("ROSTER_STUDENT_MISMATCH");
            }
            TheoryTrainingRecord record;
            if (id is Guid existing)
            {
                record = await Required<TheoryTrainingRecord>(s, existing, ct);
                if (record.LessonId != request.LessonId) throw LearningErrors.Rule("RECORD_IDENTITY_IMMUTABLE");
                if (record.Revision != request.ExpectedRevision) throw LearningErrors.Conflict();
            }
            else
            {
                if (await s.FindAsync<TheoryTrainingRecord>(x => x.LessonId == request.LessonId, ct) is not null) throw LearningErrors.Conflict("THEORY_RECORD_EXISTS");
                record = new TheoryTrainingRecord { Id = Guid.NewGuid(), SchoolId = request.SchoolId, LessonId = request.LessonId }; s.Add(record);
            }
            var old = record.CurrentVersionId is Guid previous ? await Required<TheoryRecordVersion>(s, previous, ct) : null;
            var fact = new TheoryRecordVersion { Id = Guid.NewGuid(), SchoolId = request.SchoolId, RecordId = record.Id, LessonId = record.LessonId,
                Version = (old?.Version ?? 0) + 1, SourceSnapshotId = verified.Id, TrainingMinutes = request.TrainingMinutes, DurationExplanation = request.DurationExplanation,
                Validation = Validation.Valid, CreatedBy = actor.UserId, CreatedAt = Now, CorrectionReason = request.CorrectionReason };
            s.Add(fact);
            foreach (var member in request.AttendanceRows) s.Add(new TheoryAttendance { Id = Guid.NewGuid(), SchoolId = request.SchoolId, RecordVersionId = fact.Id, EnrollmentId = member.EnrollmentId, StudentId = member.StudentId, Attendance = member.Attendance });
            record.CurrentVersionId = fact.Id; record.Revision++;
            await Touch(s, request.SchoolId, request.AttendanceRows.Select(x => x.EnrollmentId), actor, "THEORY_VERSION_CHANGED", ct,
                old is null ? InvalidationScope.ProspectiveOnly : InvalidationScope.HistoricalReviewRequired);
            await s.FlushAsync(ct); return await TheoryDto(s, record, ct);
        }, ct);
    }
    private static async Task<TheoryRecordResult> TheoryDto(ILearningSession s, TheoryTrainingRecord record, CancellationToken ct)
    {
        var fact = await Required<TheoryRecordVersion>(s, record.CurrentVersionId!.Value, ct);
        var rows = await s.ListAsync<TheoryAttendance>(x => x.RecordVersionId == fact.Id, ct);
        var sourceState = await s.FindAsync<LessonSourceState>(x => x.LessonId == record.LessonId, ct);
        var current = sourceState?.CurrentSnapshotId == fact.SourceSnapshotId && sourceState.Freshness == Freshness.Current;
        return new(record.Id, record.Revision, fact.Version, fact.ConfirmedAt is null ? "Pending" : current ? "Confirmed" : "RevalidationRequired",
            new(fact.SchoolId, fact.LessonId, fact.SourceSnapshotId, fact.TrainingMinutes, rows.OrderBy(x => x.EnrollmentId).Select(x => new AttendanceInput(x.EnrollmentId, x.StudentId, x.Attendance)).ToArray(), fact.DurationExplanation), fact.ConfirmedBy, fact.ConfirmedAt, current);
    }
    public async Task<Roster> TheoryRosterAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct)
    {
        Id(lesson); Admission(actor);
        var source = await owners.SourceAsync(school, lesson, ct);
        if (source.SchoolId != school || source.LessonId != lesson) throw LearningErrors.Missing();
        var authority = await Authority(school, actor, null, ct, source.TeacherId, source.GroupId);
        if (!authority.Administrator && !(authority.Teacher && actor.TeacherId == source.TeacherId)) throw LearningErrors.Missing();
        if (source.Kind != TrainingKind.Theory || source.GroupId is null || source.GroupMembershipRevision is null || source.ActualStart is null || source.ActualEnd is null)
            throw LearningErrors.Rule("COMPLETED_SOURCE_REQUIRED");
        var roster = await owners.RosterAsync(school, source.GroupId.Value, source.GroupMembershipRevision.Value, source.ActualStart.Value, source.ActualEnd.Value, ct);
        if (roster.SchoolId != school || roster.GroupId != source.GroupId || roster.MembershipVersion != source.GroupMembershipRevision)
            throw LearningErrors.Dependency("ROSTER_MISMATCH");
        return roster;
    }
    public async Task<TheoryRecordResult[]> TheoryRecordsAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct)
    {
        Id(lesson); Admission(actor);
        var source = await owners.SourceAsync(school, lesson, ct);
        if (source.SchoolId != school || source.LessonId != lesson) throw LearningErrors.Missing();
        var authority = await Authority(school, actor, null, ct, source.TeacherId, source.GroupId);
        if (!authority.Administrator && !(authority.Teacher && actor.TeacherId == source.TeacherId)) throw LearningErrors.Missing();
        await using var s = await store.OpenAsync(school, false, ct);
        var records = await s.ListAsync<TheoryTrainingRecord>(x => x.LessonId == lesson, ct);
        var result = new List<TheoryRecordResult>();
        foreach (var record in records) result.Add(await TheoryDto(s, record, ct));
        return result.ToArray();
    }
    public async Task<TheoryRecordResult> TheoryReadAsync(Guid school, Guid id, LearningActor actor, CancellationToken ct)
    {
        LessonSourceSnapshot source;
        await using (var read = await store.OpenAsync(school, false, ct))
        {
            var root = await Required<TheoryTrainingRecord>(read, id, ct); var version = await Required<TheoryRecordVersion>(read, root.CurrentVersionId!.Value, ct);
            source = await Required<LessonSourceSnapshot>(read, version.SourceSnapshotId, ct);
        }
        var authority = await Authority(school, actor, null, ct, source.InstructorId, source.GroupId);
        if (!authority.Administrator && !(authority.Teacher && actor.TeacherId == source.InstructorId)) throw LearningErrors.Missing();
        await using var s = await store.OpenAsync(school, false, ct); return await TheoryDto(s, await Required<TheoryTrainingRecord>(s, id, ct), ct);
    }
    public async Task<CommandResult<TheoryRecordResult>> ConfirmTheoryAsync(Guid school, Guid id, long revision, Guid operation, LearningActor actor, CancellationToken ct)
    {
        TheoryRecordResult saved = await TheoryReadAsync(school, id, actor, ct);
        var source = await FetchSource(school, saved.Facts.LessonId, actor, ct);
        var authority = await Authority(school, actor, null, ct, source.InstructorId, source.GroupId); Teacher(actor, authority, source);
        LearningRules.TeachingMinutes(source, saved.Facts.TrainingMinutes, saved.Facts.DurationExplanation);
        var roster = await owners.RosterAsync(school, source.GroupId!.Value, source.MembershipRevision!.Value, source.ActualStart!.Value, source.ActualEnd!.Value, ct);
        SameRoster(saved.Facts.AttendanceRows, roster, source);
        return await Command(school, operation, "ConfirmTheoryRecord", id, new { revision }, actor, authority, async s =>
        {
            var record = await Required<TheoryTrainingRecord>(s, id, ct);
            if (record.Revision != revision || record.Revision != saved.Revision) throw LearningErrors.Conflict();
            var fact = await Required<TheoryRecordVersion>(s, record.CurrentVersionId!.Value, ct);
            await ExactSource(s, fact.SourceSnapshotId, fact.LessonId, ct);
            if (fact.Version != saved.FactVersion || fact.SourceSnapshotId != source.Id) throw LearningErrors.Conflict("SOURCE_STALE");
            foreach (var member in saved.Facts.AttendanceRows) await Programme(s, member.EnrollmentId, ct);
            if (fact.ConfirmedAt is not null) return await TheoryDto(s, record, ct);
            fact.ConfirmedBy = actor.UserId; fact.ConfirmedAt = Now; record.Revision++;
            await Touch(s, school, saved.Facts.AttendanceRows.Select(x => x.EnrollmentId), actor, "THEORY_CONFIRMED", ct, InvalidationScope.ProspectiveOnly);
            await s.FlushAsync(ct); return await TheoryDto(s, record, ct);
        }, ct);
    }
}

