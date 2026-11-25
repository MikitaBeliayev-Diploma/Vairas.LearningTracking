namespace Vairas.LearningTracking.Domain;

public static class LearningRules
{
    public static void Require(bool value, string code) { if (!value) throw new RuleViolation(code); }

    public static bool SourceShape(LessonSourceSnapshot source)
    {
        if (source.SchoolId == Guid.Empty || source.LessonId == Guid.Empty || source.LessonRevision < 1 || source.InstructorId == Guid.Empty || !Enum.IsDefined(source.Kind)) return false;
        if ((source.ActualStart is null) != (source.ActualEnd is null)) return false;
        if (source.ActualStart is not null && source.ActualEnd <= source.ActualStart) return false;
        if (source.PhysicallyCompleted && source.SourceValid && source.ActualStart is null) return false;
        return source.Kind == TrainingKind.Practical
            ? source.EnrollmentId is Guid e && e != Guid.Empty && source.StudentId is Guid st && st != Guid.Empty && source.VehicleId is Guid v && v != Guid.Empty && source.GroupId is null && source.MembershipRevision is null
            : source.EnrollmentId is null && source.StudentId is null && source.VehicleId is null && source.GroupId is Guid g && g != Guid.Empty && source.MembershipRevision > 0;
    }
    public static bool SameSource(LessonSourceSnapshot a, LessonSourceSnapshot b) =>
        a.SchoolId == b.SchoolId && a.LessonId == b.LessonId && a.LessonRevision == b.LessonRevision && a.Kind == b.Kind && a.InstructorId == b.InstructorId &&
        a.EnrollmentId == b.EnrollmentId && a.StudentId == b.StudentId && a.VehicleId == b.VehicleId && a.GroupId == b.GroupId && a.MembershipRevision == b.MembershipRevision &&
        a.ActualStart == b.ActualStart && a.ActualEnd == b.ActualEnd && a.PhysicallyCompleted == b.PhysicallyCompleted && a.SourceValid == b.SourceValid;

    public static void TeachingMinutes(LessonSourceSnapshot source, int minutes, string? explanation)
    {
        Require(SourceShape(source) && source.PhysicallyCompleted && source.SourceValid, "COMPLETED_SOURCE_REQUIRED");
        var physical = (source.ActualEnd!.Value - source.ActualStart!.Value).TotalMinutes;
        Require(minutes > 0 && minutes <= physical, "TRAINING_MINUTES_OUTSIDE_INTERVAL");
        Require(minutes == physical || !string.IsNullOrWhiteSpace(explanation), "DURATION_EXPLANATION_REQUIRED");
    }

    public static bool PracticalEligible(PracticalTrainingRecord record, PracticalRecordVersion version, LessonSourceSnapshot source,
        LessonSourceState state, IEnumerable<PracticalRecordResponse> responses, StudentTraining training)
    {
        if (record.CurrentVersionId != version.Id || record.Id != version.RecordId || record.EnrollmentId != training.Id || version.ProgramVersionId != training.ProgramVersionId ||
            source.EnrollmentId != training.Id || source.StudentId != training.StudentId || source.Kind != TrainingKind.Practical || version.SourceSnapshotId != source.Id ||
            state.CurrentSnapshotId != source.Id || state.Freshness != Freshness.Current || version.Validation != Validation.Valid || !source.PhysicallyCompleted || !source.SourceValid ||
            version.TrainingMinutes is null || version.DistanceKm is null || version.DistanceKm < 0 || version.TrainingMinutes <= 0 || source.ActualStart is null ||
            source.ActualEnd is null || version.TrainingMinutes > (source.ActualEnd.Value - source.ActualStart.Value).TotalMinutes) return false;
        var current = responses.Where(x => x.RecordVersionId == version.Id).ToArray();
        return !current.Any(x => x.Response == ResponseKind.Dispute) && Enum.GetValues<ResponseRole>().All(role => current.Any(x => x.ActorRole == role && x.Response == ResponseKind.Confirm));
    }

    public static bool TheoryEligible(TheoryTrainingRecord record, TheoryRecordVersion version, TheoryAttendance attendance,
        LessonSourceSnapshot source, LessonSourceState state, StudentTraining training) =>
        record.CurrentVersionId == version.Id && version.RecordId == record.Id && attendance.RecordVersionId == version.Id && attendance.EnrollmentId == training.Id && attendance.StudentId == training.StudentId &&
        training.ProgramVersionId is not null && attendance.Attendance == Attendance.Present && version.Validation == Validation.Valid && version.ConfirmedAt is not null &&
        version.ConfirmedBy is not null && source.Kind == TrainingKind.Theory && version.SourceSnapshotId == source.Id && state.CurrentSnapshotId == source.Id && state.Freshness == Freshness.Current &&
        source.PhysicallyCompleted && source.SourceValid && source.ActualStart is not null && source.ActualEnd is not null && version.TrainingMinutes > 0 && version.TrainingMinutes <= (source.ActualEnd.Value - source.ActualStart.Value).TotalMinutes;

    public static void Curriculum(TrainingProgramVersion version, IReadOnlyList<AssessmentDefinitionVersion> definitions, IReadOnlyList<AdmissionRequirement> requirements,
        IReadOnlyList<ProgramRuleSource> sources, IReadOnlyList<LessonPlanningPolicy> planning, DateOnly today)
    {
        Require(version.RequiredTheoryMinutes >= 0 && version.RequiredPracticalMinutes >= 0 && version.TheoryMinutesPerUnit > 0 && version.PracticalMinutesPerUnit > 0, "INVALID_PROGRAMME_MINUTES");
        Require(version.EffectiveTo is null || version.EffectiveTo >= version.EffectiveFrom, "INVALID_EFFECTIVE_PERIOD");
        Require(version.JournalChecklistVersionId is not null && version.LegalBasisVerified && sources.Count > 0 && sources.All(s =>
            s.VerifiedBy is not null && s.VerifiedAt is not null && !string.IsNullOrWhiteSpace(s.SourceEdition) && !string.IsNullOrWhiteSpace(s.Applicability) &&
            Uri.TryCreate(s.SourceUri, UriKind.Absolute, out var uri) && uri.Scheme == "https" && s.EffectiveFrom <= version.EffectiveFrom &&
            (s.EffectiveTo is null || version.EffectiveTo is not null && s.EffectiveTo >= version.EffectiveTo)), "VERIFIED_RULES_REQUIRED");
        Require(version.EffectiveTo is null || version.EffectiveTo >= today, "PROGRAMME_EXPIRED");
        foreach (var d in definitions) Require(d.MinimumMark < d.MaximumMark && d.PassingMark >= d.MinimumMark && d.PassingMark <= d.MaximumMark, "INVALID_ASSESSMENT_SCALE");
        Require(definitions.Select(x => x.Id).Distinct().Count() == definitions.Count, "DUPLICATE_DEFINITION");
        foreach (var r in requirements)
        {
            Require(definitions.Any(d => d.Id == r.DefinitionVersionId), "REQUIREMENT_DEFINITION_MISMATCH");
            Require(Enum.IsDefined(r.Kind), "INVALID_REQUIREMENT_KIND");
            if (r.Kind is RequirementKind.MinimumTheoryMinutes or RequirementKind.MinimumPracticalMinutes)
                Require(r.MinimumMinutes >= 0 && r.PrerequisiteDefinitionVersionId is null && r.MinimumMark is null, "INVALID_MINUTE_REQUIREMENT");
            else
            {
                var prerequisite = definitions.SingleOrDefault(d => d.Id == r.PrerequisiteDefinitionVersionId);
                Require(prerequisite is not null && prerequisite.Id != r.DefinitionVersionId && r.MinimumMinutes is null, "INVALID_ASSESSMENT_PREREQUISITE");
                Require(r.Kind == RequirementKind.AssessmentPassed ? r.MinimumMark is null : r.MinimumMark >= prerequisite!.MinimumMark && r.MinimumMark <= prerequisite.MaximumMark, "INVALID_MARK_REQUIREMENT");
            }
        }
        var visiting = new HashSet<Guid>(); var visited = new HashSet<Guid>();
        void Visit(Guid id)
        {
            if (visited.Contains(id)) return;
            Require(visiting.Add(id), "CYCLIC_PREREQUISITES");
            foreach (var r in requirements.Where(x => x.DefinitionVersionId == id && x.PrerequisiteDefinitionVersionId is not null)) Visit(r.PrerequisiteDefinitionVersionId!.Value);
            visiting.Remove(id); visited.Add(id);
        }
        foreach (var definition in definitions) Visit(definition.Id);
        foreach (var kind in Enum.GetValues<TrainingKind>())
        {
            var policy = planning.SingleOrDefault(x => x.Kind == kind);
            Require(policy is not null && policy.LessonMinutes > 0 && policy.MinimumFinalLessonMinutes > 0 && policy.MinimumFinalLessonMinutes <= policy.LessonMinutes, "PLANNING_POLICY_REQUIRED");
        }
    }

    public static int[] SplitDemand(int remaining, LessonPlanningPolicy policy)
    {
        Require(remaining >= 0 && policy.LessonMinutes > 0 && policy.MinimumFinalLessonMinutes > 0 && policy.MinimumFinalLessonMinutes <= policy.LessonMinutes, "INVALID_PLANNING_POLICY");
        var full = remaining / policy.LessonMinutes; var rest = remaining % policy.LessonMinutes;
        Require(full <= 1000, "PLANNING_LIMIT_EXCEEDED");
        Require(rest == 0 || policy.AllowShortFinalLesson && rest >= policy.MinimumFinalLessonMinutes, "REMAINDER_NOT_PLANNABLE");
        return Enumerable.Repeat(policy.LessonMinutes, full).Concat(rest == 0 ? [] : new[] { rest }).ToArray();
    }

    public static ResultStatus HistoricalStatus(AssessmentResultVersion result, ReadinessDecision? decision)
    {
        if (result.Status != ResultStatus.Recorded) return result.Status;
        if (result.ReadinessDecisionId is null) return ResultStatus.Recorded;
        if (decision is null || decision.Decision != Decision.Approved || decision.DecidedAt > result.AssessedAt) return ResultStatus.ReviewRequired;
        if (decision.InvalidatedAt is null) return ResultStatus.Recorded;
        if (decision.InvalidatedAt <= result.AssessedAt) return ResultStatus.ReviewRequired;
        if (decision.InvalidationScope == InvalidationScope.ProspectiveOnly && decision.InvalidationEffectiveAt > result.AssessedAt) return ResultStatus.Recorded;
        return result.ReviewedInvalidationAt == decision.InvalidatedAt ? ResultStatus.Recorded : ResultStatus.ReviewRequired;
    }

    public static decimal? Percent(int theory, int practical, int requiredTheory, int requiredPractical)
    {
        var denominator = (long)requiredTheory + requiredPractical;
        return denominator == 0 ? null : decimal.Round(100m * (Math.Min(theory, requiredTheory) + (long)Math.Min(practical, requiredPractical)) / denominator, 2);
    }
}
