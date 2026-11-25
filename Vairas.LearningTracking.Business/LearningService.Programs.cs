using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public sealed partial class LearningService
{
    private static ProgramResponse ProgramDto(TrainingProgram p) => new(p.Id, p.SchoolId, p.CategoryCode, p.Name, p.Revision);
    public async Task<CommandResult<ProgramResponse>> CreateProgramAsync(CreateProgramRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Text(request.Name, 200); Text(request.CategoryCode, 16);
        var authority = await Authority(request.SchoolId, actor, null, ct); Admin(authority);
        return await Command(request.SchoolId, operation, "CreateProgram", null, request, actor, authority, s =>
        {
            var program = new TrainingProgram { Id = Guid.NewGuid(), SchoolId = request.SchoolId, CategoryCode = request.CategoryCode.Trim(), Name = request.Name.Trim(), Revision = 1, CreatedAt = Now };
            s.Add(program); return Task.FromResult(ProgramDto(program));
        }, ct);
    }
    public async Task<ProgramResponse[]> ProgramsAsync(Guid school, LearningActor actor, CancellationToken ct)
    {
        var authority = await Authority(school, actor, null, ct); Admin(authority);
        await using var s = await store.OpenAsync(school, false, ct);
        return (await s.ListAsync<TrainingProgram>(x => true, ct)).OrderBy(x => x.Name).Select(ProgramDto).ToArray();
    }
    public async Task<ProgramVersionResponse[]> ProgramVersionsAsync(Guid school, Guid program, LearningActor actor, CancellationToken ct)
    {
        var authority = await Authority(school, actor, null, ct); Admin(authority);
        await using var s = await store.OpenAsync(school, false, ct);
        var root = await Required<TrainingProgram>(s, program, ct);
        var versions = await s.ListAsync<TrainingProgramVersion>(x => x.ProgramId == program, ct);
        var result = new List<ProgramVersionResponse>();
        foreach (var version in versions.OrderByDescending(x => x.Version))
            result.Add(await VersionDto(s, root, version, ct));
        return result.ToArray();
    }
    private static void ValidateDraft(ProgramVersionRequest r)
    {
        Text(r.PathwayCode, 80); Id(r.ChecklistVersionId);
        if (r.Topics is null || r.Assessments is null || r.Sources is null || r.PlanningPolicies is null || r.Topics.Length > 500 || r.Assessments.Length > 100 || r.Sources.Length > 100 || r.PlanningPolicies.Length != 2) throw LearningErrors.Invalid();
        if (r.Topics.Any(x => x is null) || r.Assessments.Any(x => x is null || x.Requirements is null || x.TopicIds is null) || r.Sources.Any(x => x is null) || r.PlanningPolicies.Any(x => x is null)) throw LearningErrors.Invalid();
        if (r.Topics.Select(x => x.TopicId).Distinct().Count() != r.Topics.Length || r.Topics.Select(x => x.Key).Distinct().Count() != r.Topics.Length ||
            r.Assessments.Select(x => x.DefinitionVersionId).Distinct().Count() != r.Assessments.Length || r.Assessments.Select(x => x.Code).Distinct().Count() != r.Assessments.Length ||
            r.Sources.Select(x => x.SourceId).Distinct().Count() != r.Sources.Length || r.PlanningPolicies.Select(x => x.Kind).Distinct().Count() != 2) throw LearningErrors.Invalid("DUPLICATE_CURRICULUM_IDENTITY");
        foreach (var t in r.Topics) { Id(t.TopicId); Text(t.Key, 80); Text(t.Name, 200); }
        foreach (var d in r.Assessments)
        {
            Id(d.DefinitionId); Id(d.DefinitionVersionId); Text(d.Code, 80); Text(d.Name, 200);
            if (!Enum.IsDefined(d.TrainingKind) || !Enum.IsDefined(d.Stage) || d.TopicIds.Any(x => !r.Topics.Any(t => t.TopicId == x)) || d.TopicIds.Distinct().Count() != d.TopicIds.Length || d.Requirements.Length > 100) throw LearningErrors.Invalid();
            if (d.Requirements.Any(x => x is null) || d.Requirements.Select(x => x.Code).Distinct().Count() != d.Requirements.Length) throw LearningErrors.Invalid();
            foreach (var requirement in d.Requirements) { Id(requirement.RequirementId); Text(requirement.Code, 80); }
        }
        foreach (var source in r.Sources) { Id(source.SourceId); Text(source.RuleCode, 80); Text(source.SourceUri, 2000); Text(source.Edition, 200); Text(source.Applicability); Text(source.UnitDescription, 120); }
    }
    public async Task<CommandResult<ProgramVersionResponse>> SaveVersionAsync(Guid program, Guid? versionId, ProgramVersionRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(program); ValidateDraft(request);
        var authority = await Authority(request.SchoolId, actor, null, ct); Admin(authority);
        return await Command(request.SchoolId, operation, versionId is null ? "CreateProgramVersion" : "EditProgramDraft", versionId ?? program, request, actor, authority, async s =>
        {
            var root = await Required<TrainingProgram>(s, program, ct);
            if (root.Revision != request.ExpectedRevision) throw LearningErrors.Conflict();
            TrainingProgramVersion v;
            if (versionId is Guid existing)
            {
                v = await Required<TrainingProgramVersion>(s, existing, ct);
                if (v.ProgramId != program || v.Status != ProgramStatus.Draft) throw LearningErrors.Conflict("PUBLISHED_CURRICULUM_IMMUTABLE");
                foreach (var x in await s.ListAsync<AdmissionRequirement>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                foreach (var x in await s.ListAsync<AssessmentTopic>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                foreach (var x in await s.ListAsync<AssessmentDefinitionVersion>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                foreach (var x in await s.ListAsync<TrainingTopic>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                foreach (var x in await s.ListAsync<ProgramRuleSource>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                foreach (var x in await s.ListAsync<LessonPlanningPolicy>(x => x.ProgramVersionId == existing, ct)) s.Remove(x);
                await s.FlushAsync(ct);
            }
            else
            {
                var prior = await s.ListAsync<TrainingProgramVersion>(x => x.ProgramId == program, ct);
                v = new TrainingProgramVersion { Id = Guid.NewGuid(), SchoolId = request.SchoolId, ProgramId = program, Version = prior.Count == 0 ? 1 : prior.Max(x => x.Version) + 1, CreatedBy = actor.UserId, CreatedAt = Now };
                s.Add(v);
            }
            v.PathwayCode = request.PathwayCode; v.RequiredTheoryMinutes = request.RequiredTheoryMinutes; v.RequiredPracticalMinutes = request.RequiredPracticalMinutes;
            v.TheoryMinutesPerUnit = request.TheoryMinutesPerUnit; v.PracticalMinutesPerUnit = request.PracticalMinutesPerUnit; v.EffectiveFrom = request.EffectiveFrom;
            v.EffectiveTo = request.EffectiveTo; v.JournalChecklistVersionId = request.ChecklistVersionId; v.LegalBasisVerified = request.Sources.Length > 0 && request.Sources.All(x => x.Verified);
            // The SQL-owned deferred FK graph is not an EF navigation graph. Persist the Draft parent before its immediate immutability guards inspect children.
            await s.FlushAsync(ct);
            foreach (var t in request.Topics) s.Add(new TrainingTopic { Id = t.TopicId, SchoolId = v.SchoolId, ProgramVersionId = v.Id, TopicKey = t.Key, Name = t.Name, Description = t.Description });
            foreach (var x in request.Sources) s.Add(new ProgramRuleSource { Id = x.SourceId, SchoolId = v.SchoolId, ProgramVersionId = v.Id, RuleCode = x.RuleCode, SourceUri = x.SourceUri, SourceEdition = x.Edition,
                Applicability = x.Applicability, UnitDescription = x.UnitDescription, EffectiveFrom = x.EffectiveFrom, EffectiveTo = x.EffectiveTo, VerifiedBy = x.Verified ? actor.UserId : null, VerifiedAt = x.Verified ? Now : null });
            foreach (var d in request.Assessments)
            {
                var stable = await s.FindAsync<AssessmentDefinition>(x => x.Id == d.DefinitionId, ct);
                if (stable is null) s.Add(new AssessmentDefinition { Id = d.DefinitionId, SchoolId = v.SchoolId, ProgramId = program, Code = d.Code });
                else if (stable.ProgramId != program || stable.Code != d.Code) throw LearningErrors.Conflict("ASSESSMENT_IDENTITY_MISMATCH");
                s.Add(new AssessmentDefinitionVersion { Id = d.DefinitionVersionId, SchoolId = v.SchoolId, ProgramId = program, ProgramVersionId = v.Id, DefinitionId = d.DefinitionId,
                    Name = d.Name, TrainingKind = d.TrainingKind, Stage = d.Stage, MinimumMark = d.MinimumMark, MaximumMark = d.MaximumMark, PassingMark = d.PassingMark, RequiredForCompletion = d.RequiredForCompletion });
                foreach (var topic in d.TopicIds) s.Add(new AssessmentTopic { Id = Guid.NewGuid(), SchoolId = v.SchoolId, ProgramVersionId = v.Id, DefinitionVersionId = d.DefinitionVersionId, TopicId = topic });
                foreach (var r in d.Requirements) s.Add(new AdmissionRequirement { Id = r.RequirementId, SchoolId = v.SchoolId, ProgramVersionId = v.Id, DefinitionVersionId = d.DefinitionVersionId,
                    Code = r.Code, Kind = r.Kind, MinimumMinutes = r.MinimumMinutes, MinimumMark = r.MinimumMark, PrerequisiteDefinitionVersionId = r.PrerequisiteDefinitionVersionId });
            }
            foreach (var p in request.PlanningPolicies) s.Add(new LessonPlanningPolicy { Id = Guid.NewGuid(), SchoolId = v.SchoolId, ProgramVersionId = v.Id, Kind = p.Kind,
                LessonMinutes = p.LessonMinutes, AllowShortFinalLesson = p.AllowShortFinalLesson, MinimumFinalLessonMinutes = p.MinimumFinalLessonMinutes });
            root.Revision++;
            await s.FlushAsync(ct);
            return await VersionDto(s, root, v, ct);
        }, ct);
    }
    private static async Task<ProgramVersionResponse> VersionDto(ILearningSession s, TrainingProgram p, TrainingProgramVersion v, CancellationToken ct)
    {
        var topics = await s.ListAsync<TrainingTopic>(x => x.ProgramVersionId == v.Id, ct);
        var definitions = await s.ListAsync<AssessmentDefinitionVersion>(x => x.ProgramVersionId == v.Id, ct);
        var stable = await s.ListAsync<AssessmentDefinition>(x => x.ProgramId == p.Id, ct);
        var assessmentTopics = await s.ListAsync<AssessmentTopic>(x => x.ProgramVersionId == v.Id, ct);
        var requirements = await s.ListAsync<AdmissionRequirement>(x => x.ProgramVersionId == v.Id, ct);
        var sources = await s.ListAsync<ProgramRuleSource>(x => x.ProgramVersionId == v.Id, ct);
        var planning = await s.ListAsync<LessonPlanningPolicy>(x => x.ProgramVersionId == v.Id, ct);
        var curriculum = new ProgramVersionRequest(p.SchoolId, p.Revision, v.PathwayCode, v.RequiredTheoryMinutes, v.RequiredPracticalMinutes, v.TheoryMinutesPerUnit, v.PracticalMinutesPerUnit,
            v.EffectiveFrom, v.EffectiveTo, v.JournalChecklistVersionId ?? Guid.Empty, topics.OrderBy(x => x.TopicKey).Select(x => new TopicInput(x.Id, x.TopicKey, x.Name, x.Description)).ToArray(),
            definitions.OrderBy(x => x.Name).Select(d => new DefinitionInput(d.DefinitionId, d.Id, stable.Single(x => x.Id == d.DefinitionId).Code, d.Name, d.TrainingKind, d.Stage, d.MinimumMark, d.MaximumMark, d.PassingMark,
                d.RequiredForCompletion, assessmentTopics.Where(x => x.DefinitionVersionId == d.Id).Select(x => x.TopicId).Order().ToArray(),
                requirements.Where(x => x.DefinitionVersionId == d.Id).OrderBy(x => x.Code).Select(x => new RequirementInput(x.Id, x.Code, x.Kind, x.MinimumMinutes, x.PrerequisiteDefinitionVersionId, x.MinimumMark)).ToArray())).ToArray(),
            sources.OrderBy(x => x.RuleCode).Select(x => new RuleSourceInput(x.Id, x.RuleCode, x.SourceUri, x.SourceEdition, x.Applicability, x.EffectiveFrom, x.EffectiveTo, x.UnitDescription, x.VerifiedAt is not null)).ToArray(),
            planning.OrderBy(x => x.Kind).Select(x => new PlanningPolicyInput(x.Kind, x.LessonMinutes, x.AllowShortFinalLesson, x.MinimumFinalLessonMinutes)).ToArray());
        return new(v.Id, p.Id, p.SchoolId, v.Version, v.Status, p.Revision, curriculum);
    }
    public async Task<DefinitionInput[]> EnrollmentDefinitionsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct)
    {
        Id(enrollment); await Authority(school, actor, enrollment, ct);
        await using var s = await store.OpenAsync(school, false, ct);
        var training = await Required<StudentTraining>(s, enrollment, ct);
        if (training.ProgramVersionId is not Guid id) throw LearningErrors.Rule("PROGRAMME_NOT_ASSIGNED");
        var version = await Required<TrainingProgramVersion>(s, id, ct);
        var program = await Required<TrainingProgram>(s, version.ProgramId, ct);
        return (await VersionDto(s, program, version, ct)).Curriculum.Assessments;
    }
    public async Task<ProgramVersionResponse> VersionAsync(Guid school, Guid version, LearningActor actor, CancellationToken ct)
    {
        var authority = await Authority(school, actor, null, ct); Admin(authority);
        await using var s = await store.OpenAsync(school, false, ct);
        var v = await Required<TrainingProgramVersion>(s, version, ct); return await VersionDto(s, await Required<TrainingProgram>(s, v.ProgramId, ct), v, ct);
    }
    public async Task<CommandResult<ProgramVersionResponse>> LifecycleAsync(Guid school, Guid version, string action, RevisionRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        if (action is not ("publish" or "retire")) throw LearningErrors.Invalid();
        var authority = await Authority(school, actor, null, ct); Admin(authority);
        return await Command(school, operation, action, version, request, actor, authority, async s =>
        {
            var v = await Required<TrainingProgramVersion>(s, version, ct); var p = await Required<TrainingProgram>(s, v.ProgramId, ct);
            if (p.Revision != request.ExpectedRevision) throw LearningErrors.Conflict();
            if (action == "publish")
            {
                if (v.Status != ProgramStatus.Draft) throw LearningErrors.Conflict("NOT_DRAFT");
                LearningRules.Curriculum(v, await s.ListAsync<AssessmentDefinitionVersion>(x => x.ProgramVersionId == version, ct), await s.ListAsync<AdmissionRequirement>(x => x.ProgramVersionId == version, ct),
                    await s.ListAsync<ProgramRuleSource>(x => x.ProgramVersionId == version, ct), await s.ListAsync<LessonPlanningPolicy>(x => x.ProgramVersionId == version, ct), Today);
                var policy = Policy(p, v) ?? throw LearningErrors.Rule("VERIFIED_CHECKLIST_REQUIRED");
                var attestation = await s.FindAsync<PolicyAttestation>(x => x.Id == policy.VersionId, ct);
                var checksum = Hash(Wire.Save(policy));
                if (attestation is not null && attestation.Checksum != checksum) throw LearningErrors.Rule("CHECKLIST_VERSION_CONTENT_CHANGED");
                if (attestation is null) s.Add(new PolicyAttestation { Id = policy.VersionId, SchoolId = school, Checksum = checksum, AttestedAt = Now });
                var programs = await s.ListAsync<TrainingProgram>(x => x.CategoryCode == p.CategoryCode, ct); var ids = programs.Select(x => x.Id).ToArray();
                var published = await s.ListAsync<TrainingProgramVersion>(x => ids.Contains(x.ProgramId) && x.PathwayCode == v.PathwayCode && x.Status == ProgramStatus.Published, ct);
                if (published.Any(x => (x.EffectiveTo is null || x.EffectiveTo >= v.EffectiveFrom) && (v.EffectiveTo is null || v.EffectiveTo >= x.EffectiveFrom))) throw LearningErrors.Conflict("PUBLISHED_PERIOD_OVERLAP");
                v.Status = ProgramStatus.Published; v.PublishedBy = actor.UserId; v.PublishedAt = Now;
            }
            else
            {
                Text(request.Reason);
                if (v.Status != ProgramStatus.Published) throw LearningErrors.Conflict("NOT_PUBLISHED");
                v.Status = ProgramStatus.Retired; v.RetiredAt = Now;
            }
            p.Revision++; await s.FlushAsync(ct); return await VersionDto(s, p, v, ct);
        }, ct);
    }
    public async Task<CommandResult<TrainingEnrollmentResponse>> StartAsync(StartTrainingRequest request, Guid operation, LearningActor actor, CancellationToken ct)
    {
        Id(request.EnrollmentId); Id(request.ProgramVersionId);
        var authority = await Authority(request.SchoolId, actor, request.EnrollmentId, ct); Admin(authority); Active(authority);
        return await Command(request.SchoolId, operation, "StartTraining", request.EnrollmentId, request, actor, authority, async s =>
        {
            var version = await Required<TrainingProgramVersion>(s, request.ProgramVersionId, ct); var program = await Required<TrainingProgram>(s, version.ProgramId, ct);
            var policy = Policy(program, version);
            var attestation = policy is null ? null : await s.FindAsync<PolicyAttestation>(x => x.Id == policy.VersionId, ct);
            if (version.Status != ProgramStatus.Published || version.EffectiveFrom > Today || version.EffectiveTo < Today || !version.LegalBasisVerified || Policy(program, version) is null || program.CategoryCode != authority.Category)
                throw LearningErrors.Rule("PROGRAMME_NOT_APPLICABLE");
            if (policy is null || attestation?.Checksum != Hash(Wire.Save(policy))) throw LearningErrors.Rule("CHECKLIST_VERSION_CONTENT_CHANGED");
            if (authority.ProgramVersionId != request.ProgramVersionId || authority.GroupId is null || authority.StudentId is null) throw LearningErrors.Rule("VERIFIED_SCHOOL_ASSOCIATION_REQUIRED");
            var training = await s.FindAsync<StudentTraining>(x => x.Id == request.EnrollmentId, ct);
            if (training is null)
            {
                training = new StudentTraining { Id = request.EnrollmentId, SchoolId = request.SchoolId, StudentId = authority.StudentId.Value, CategoryCode = authority.Category, CreatedAt = Now, DependencyRevision = 1 };
                s.Add(training);
            }
            if (training.ProgramVersionId is not null && training.ProgramVersionId != version.Id) throw LearningErrors.Conflict("PROGRAMME_TRANSFER_UNSUPPORTED");
            if (training.StudentId != authority.StudentId || training.CategoryCode != authority.Category) throw LearningErrors.Conflict("ENROLLMENT_BINDING_CHANGED");
            if (training.ProgramVersionId is null) { training.ProgramVersionId = version.Id; training.ProgramAssignedBy = actor.UserId; training.ProgramAssignedAt = Now; }
            return new TrainingEnrollmentResponse(training.Id, training.SchoolId, training.StudentId, training.ProgramVersionId, training.DependencyRevision);
        }, ct);
    }
}


