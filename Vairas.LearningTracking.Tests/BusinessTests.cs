using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Unit")]
public sealed class BusinessTests
{
    [Test]
    public async Task Publication_binding_and_draft_history_are_explicit()
    {
        var w=new TestWorld();await w.Published();
        var programs=await w.Service.ProgramsAsync(w.School,w.Admin,default);Assert.That(programs.Single().ProgramId,Is.EqualTo(w.Program));
        var versions=await w.Service.ProgramVersionsAsync(w.School,w.Program,w.Admin,default); Assert.That(versions.Select(x=>x.ProgramVersionId), Does.Contain(w.ProgramVersion));
        var v=await w.Service.VersionAsync(w.School,w.ProgramVersion,w.Admin,default);Assert.That(v.Status,Is.EqualTo(ProgramStatus.Published));
        Assert.That(v.Curriculum.Assessments,Has.Length.EqualTo(2));
        var replay=(await w.Service.StartAsync(new(w.School,w.Enrollment,w.ProgramVersion),Guid.NewGuid(),w.Admin,default)).Value;Assert.That(replay.EnrollmentId,Is.EqualTo(w.Enrollment));
        Business.ThrowsBusinessAsync(async()=>await w.Service.SaveVersionAsync(w.Program,w.ProgramVersion,v.Curriculum,Guid.NewGuid(),w.Admin,default));
        var retired=await w.Service.LifecycleAsync(w.School,w.ProgramVersion,"retire",new(v.Revision,"Replaced for future enrollments"),Guid.NewGuid(),w.Admin,default);
        Assert.That(retired.Value.Status,Is.EqualTo(ProgramStatus.Retired));
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);Assert.That(progress.SourcesVerified,Is.True,"Retirement alone preserves pinned historical applicability.");
    }
    [Test]
    public async Task Draft_replacement_preserves_stable_assessment_identity_and_requires_revision()
    {
        var w=new TestWorld();var p=(await w.Service.CreateProgramAsync(new(w.School,"B","Draft"),Guid.NewGuid(),w.Admin,default)).Value;
        var input=w.Curriculum();var first=(await w.Service.SaveVersionAsync(p.ProgramId,null,input,Guid.NewGuid(),w.Admin,default)).Value;
        var edited=await w.Service.SaveVersionAsync(p.ProgramId,first.ProgramVersionId,input with{ExpectedRevision=first.Revision,RequiredPracticalMinutes=120},Guid.NewGuid(),w.Admin,default);
        Assert.That(edited.Value.Curriculum.RequiredPracticalMinutes,Is.EqualTo(120));
        Business.ThrowsBusinessAsync(async()=>await w.Service.SaveVersionAsync(p.ProgramId,first.ProgramVersionId,input,Guid.NewGuid(),w.Admin,default));
    }
    [Test]
    public async Task Idempotency_is_bound_to_actor_command_and_payload()
    {
        var w=new TestWorld();var key=Guid.NewGuid();var request=new CreateProgramRequest(w.School,"B","Programme");
        var first=await w.Service.CreateProgramAsync(request,key,w.Admin,default);var retry=await w.Service.CreateProgramAsync(request,key,w.Admin,default);
        Assert.That(retry.Replayed,Is.True);Assert.That(retry.Value,Is.EqualTo(first.Value));
        var error=Business.ThrowsBusinessAsync(async()=>await w.Service.CreateProgramAsync(request with{Name="Changed"},key,w.Admin,default));Assert.That(error!.Code,Is.EqualTo("IDEMPOTENCY_KEY_REUSED"));
        Assert.That(w.Memory.Rows.OfType<TrainingProgram>().Count(),Is.EqualTo(1));
    }
    [Test]
    public async Task Unknown_school_and_expired_admission_never_apply_commands()
    {
        var w=new TestWorld();
        Business.ThrowsBusinessAsync(async()=>await w.Service.CreateProgramAsync(new(Guid.NewGuid(),"B","Denied"),Guid.NewGuid(),w.Admin,default));
        Business.ThrowsBusinessAsync(async()=>await w.Service.CreateProgramAsync(new(w.School,"B","Denied"),Guid.NewGuid(),w.Admin with{ValidUntil=w.Clock.Now},default));
        Business.ThrowsBusinessAsync(async()=>await w.Service.CreateProgramAsync(new(w.School,"B","Denied"),Guid.NewGuid(),w.Learner,default));
        Assert.That(w.Memory.Rows,Is.Empty);
    }
    [Test]
    public async Task Practical_requires_same_version_dual_confirmation_and_counts_once()
    {
        var w=new TestWorld();await w.Published();var record=await w.Practical();
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
        var first=(await w.Service.RespondAsync(w.School,record.RecordId,record.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
        var key=Guid.NewGuid();var confirmed=await w.Service.RespondAsync(w.School,record.RecordId,first.Revision,ResponseKind.Confirm,null,key,w.Learner,default);
        var retry=await w.Service.RespondAsync(w.School,record.RecordId,first.Revision,ResponseKind.Confirm,null,key,w.Learner,default);
        Assert.That(retry.Replayed,Is.True);Assert.That(confirmed.Value.Status,Is.EqualTo("Confirmed"));
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);Assert.That(progress.PracticalMinutes,Is.EqualTo(60));Assert.That(progress.Percent,Is.EqualTo(50));
        Assert.That((await w.Service.PracticalReadAsync(w.School,record.RecordId,w.Admin,default)).Responses,Has.Length.EqualTo(2));
        Assert.That((await w.Service.ConfirmedAsync(w.School,w.Enrollment,default)).Records.Single().ActualMinutes,Is.EqualTo(60));
    }
    [Test]
    public async Task Administrator_cannot_sign_and_dispute_permanently_closes_version()
    {
        var w=new TestWorld();await w.Published();var record=await w.Practical(true);
        Business.ThrowsBusinessAsync(async()=>await w.Service.RespondAsync(w.School,record.RecordId,record.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),w.Admin,default));
        var disputed=(await w.Service.RespondAsync(w.School,record.RecordId,record.Revision,ResponseKind.Dispute,"Distance is incorrect",Guid.NewGuid(),w.Learner,default)).Value;
        Assert.That(disputed.Status,Is.EqualTo("Disputed"));
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
        Business.ThrowsBusinessAsync(async()=>await w.Service.RespondAsync(w.School,record.RecordId,disputed.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),w.Instructor,default));
        var fixedRecord=(await w.Service.PracticalAsync(record.RecordId,record.Facts with{ExpectedRevision=disputed.Revision,CorrectionReason="Correct distance",DistanceKm=13},Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(fixedRecord.FactVersion,Is.EqualTo(2));Assert.That(fixedRecord.Responses,Is.Empty);
    }
    [Test]
    public async Task Shorter_teaching_duration_needs_explanation_and_invalid_topics_do_not_persist()
    {
        var w=new TestWorld();await w.Published();var source=await w.Service.VerifySourceAsync(w.School,w.PracticalLesson,w.Instructor,default);
        var input=new PracticalFactsRequest(w.School,w.Enrollment,w.PracticalLesson,w.ProgramVersion,source.SourceSnapshotId,50,2,[w.Topic]);
        Assert.ThrowsAsync<RuleViolation>(async()=>await w.Service.PracticalAsync(null,input,Guid.NewGuid(),w.Instructor,default));
        Business.ThrowsBusinessAsync(async()=>await w.Service.PracticalAsync(null,input with{DurationExplanation="Break",TopicIds=[Guid.NewGuid()]},Guid.NewGuid(),w.Instructor,default));
        Assert.That(w.Memory.Rows.OfType<PracticalTrainingRecord>(),Is.Empty);
        var accepted=await w.Service.PracticalAsync(null,input with{DurationExplanation="Break excluded"},Guid.NewGuid(),w.Instructor,default);Assert.That(accepted.Value.Facts.TrainingMinutes,Is.EqualTo(50));
    }
    [TestCase(Attendance.Present,60)]
    [TestCase(Attendance.Absent,0)]
    public async Task Theory_requires_explicit_teacher_confirmation(Attendance attendance,int expected)
    {
        var w=new TestWorld();await w.Published();var record=await w.Theory(false,attendance);
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).TheoryMinutes,Is.Zero);
        Business.ThrowsBusinessAsync(async()=>await w.Service.TheoryReadAsync(w.School,record.RecordId,w.Learner,default));
        var confirmed=(await w.Service.ConfirmTheoryAsync(w.School,record.RecordId,record.Revision,Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(confirmed.ConfirmedBy,Is.EqualTo(w.TeacherUser));
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).TheoryMinutes,Is.EqualTo(expected));
        var corrected=(await w.Service.TheoryAsync(record.RecordId,record.Facts with{ExpectedRevision=confirmed.Revision,CorrectionReason="Correct attendance",AttendanceRows=[new(w.Enrollment,w.Student,Attendance.Absent)]},Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(corrected.ConfirmedAt,Is.Null);Assert.That(corrected.FactVersion,Is.EqualTo(2));
    }
    [Test]
    public async Task Wrong_or_unavailable_historical_roster_cannot_confirm()
    {
        var w=new TestWorld();await w.Published();var record=await w.Theory();
        w.RosterOverride=new(w.Group,w.School,1,[new(w.Enrollment,Guid.NewGuid())]);
        Business.ThrowsBusinessAsync(async()=>await w.Service.ConfirmTheoryAsync(w.School,record.RecordId,record.Revision,Guid.NewGuid(),w.Instructor,default));
        Assert.That(w.Memory.Rows.OfType<TheoryRecordVersion>().Single().ConfirmedAt,Is.Null);
    }
    [Test]
    public async Task Source_correction_withdraws_credit_and_terminally_invalidates_readiness()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);var approval=await w.Approved();
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2,ActualEnd=w.Sources[w.PracticalLesson].ActualEnd!.Value.AddMinutes(-10)};
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);Assert.That(progress.PracticalMinutes,Is.Zero);
        var readiness=(await w.Service.ReadinessAsync(w.School,w.Enrollment,w.Admin,default)).Single();Assert.That(readiness.Invalidated,Is.True);
        Assert.That(w.Memory.Rows.OfType<ReadinessDecision>().Single(x=>x.Id==approval.DecisionId).InvalidatedAt,Is.Not.Null);
    }
    [Test]
    public async Task Held_result_exposes_the_authoritative_invalidation_time_and_review_must_echo_it_exactly()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);var approval=await w.Approved();
        w.Clock.Now=w.Clock.Now.AddSeconds(1);
        var assessed=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.FinalDefinition,8,w.Clock.Now,approval.DecisionId),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(assessed.ReadinessInvalidatedAt,Is.Null);
        w.Clock.Now=w.Clock.Now.AddMinutes(5);
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2,ActualEnd=w.Sources[w.PracticalLesson].ActualEnd!.Value.AddMinutes(-10)};
        var held=(await w.Service.AssessmentsAsync(w.School,w.Enrollment,w.Admin,default)).Single();
        var invalidatedAt=w.Memory.Rows.OfType<ReadinessDecision>().Single(x=>x.Id==approval.DecisionId).InvalidatedAt;
        Assert.That(held.Status,Is.EqualTo(ResultStatus.ReviewRequired));
        Assert.That(invalidatedAt,Is.Not.Null);
        Assert.That(held.ReadinessInvalidatedAt,Is.EqualTo(invalidatedAt),"the read contract must expose the exact authoritative timestamp");
        var readiness=(await w.Service.ReadinessAsync(w.School,w.Enrollment,w.Admin,default)).Single();
        Assert.That(readiness.Invalidated,Is.True);
        var wrong=Business.ThrowsBusinessAsync(async()=>await w.Service.CorrectAssessmentAsync(w.School,held.AssessmentId,
            new(held.Revision,held.Mark,ResultStatus.Recorded,"Reviewed",ReviewedInvalidationAt:invalidatedAt!.Value.AddTicks(1)),Guid.NewGuid(),w.Instructor,default));
        Assert.That(wrong.Code,Is.EqualTo("EXACT_HISTORICAL_REVIEW_REQUIRED"));
        var reviewed=(await w.Service.CorrectAssessmentAsync(w.School,held.AssessmentId,
            new(held.Revision,held.Mark,ResultStatus.Recorded,"Reviewed after readiness invalidation",ReviewedInvalidationAt:held.ReadinessInvalidatedAt),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(reviewed.Status,Is.EqualTo(ResultStatus.Recorded));
        Assert.That(reviewed.ReviewedInvalidationAt,Is.EqualTo(invalidatedAt));
        var history=await w.Service.AssessmentHistoryAsync(w.School,held.AssessmentId,w.Admin,default);
        Assert.That(history.Select(x=>x.Version),Is.EqualTo(new[]{1,2}));Assert.That(history[1].ReviewedInvalidationAt,Is.EqualTo(invalidatedAt));
    }
    [Test]
    public async Task Owner_outage_blocks_release_and_recovery_does_not_reactivate_old_approval()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);await w.Approved();
        w.SourceAvailable=false;
        var held=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Admin,default);Assert.That(held.SourcesVerified,Is.False);Assert.That(held.PracticalMinutes,Is.Zero);
        w.SourceAvailable=true;
        var restored=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Admin,default);Assert.That(restored.PracticalMinutes,Is.EqualTo(60));
        Assert.That((await w.Service.ReadinessAsync(w.School,w.Enrollment,w.Admin,default)).Single().Invalidated,Is.True);
    }
    [Test]
    public async Task Readiness_requires_current_prerequisites_and_a_separate_assessor()
    {
        var w=new TestWorld();await w.Published();
        var incomplete=(await w.Service.EvaluateAsync(w.School,w.Enrollment,new(w.FinalDefinition),Guid.NewGuid(),w.Learner,default)).Value;
        Assert.That(incomplete.FormalStatus,Is.EqualTo(FormalStatus.Incomplete));
        Business.ThrowsBusinessAsync(async()=>await w.Service.DecideAsync(w.School,w.Enrollment,new(incomplete.EvaluationId,Decision.Approved,"Not enough",incomplete.Revision),Guid.NewGuid(),w.Instructor,default));
        var declined=(await w.Service.DecideAsync(w.School,w.Enrollment,new(incomplete.EvaluationId,Decision.NotApproved,"Needs practice",incomplete.Revision),Guid.NewGuid(),w.Instructor,default)).Value;Assert.That(declined.Decision,Is.EqualTo(Decision.NotApproved));
        await w.Practical(true);await w.Theory(true);
        var evaluation=(await w.Service.EvaluateAsync(w.School,w.Enrollment,new(w.FinalDefinition),Guid.NewGuid(),w.Learner,default)).Value;
        Assert.That(evaluation.Status,Is.EqualTo("AwaitingAssessor"));
        Business.ThrowsBusinessAsync(async()=>await w.Service.DecideAsync(w.School,w.Enrollment,new(evaluation.EvaluationId,Decision.Approved,"Admin cannot assess",evaluation.Revision),Guid.NewGuid(),w.Admin,default));
        var approved=(await w.Service.DecideAsync(w.School,w.Enrollment,new(evaluation.EvaluationId,Decision.Approved,"Competence observed",evaluation.Revision),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(approved.Status,Is.EqualTo("Approved"));
    }
    [Test]
    public async Task End_to_end_completion_and_manual_declaration_never_send_external_data()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);var approval=await w.Approved();
        w.Clock.Now=w.Clock.Now.AddSeconds(1);
        var assessment=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.FinalDefinition,8,w.Clock.Now,approval.DecisionId),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(assessment.Status,Is.EqualTo(ResultStatus.Recorded));Assert.That(assessment.Outcome,Is.EqualTo(Outcome.Passed));
        var prepared=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.CompletionReport),Guid.NewGuid(),w.Admin,default)).Value;
        Assert.That(prepared.Status,Is.EqualTo(EvidenceStatus.Ready));
        var read=await w.Service.EvidenceAsync(w.School,prepared.SnapshotId,w.Learner,default);Assert.That(read.Current,Is.True);
        var completion=await w.Service.CompletionAsync(w.School,w.Enrollment,default);Assert.That(completion.SnapshotId,Is.EqualTo(prepared.SnapshotId));
        var declaration=await w.Service.DeclareAsync(w.School,w.Enrollment,new(prepared.SnapshotId,"Administrator reports manual handoff"),Guid.NewGuid(),w.Admin,default);
        Assert.That(declaration.Value.SnapshotId,Is.EqualTo(prepared.SnapshotId));
        Assert.That(w.Memory.Rows.OfType<ReportingDeclaration>().Count(),Is.EqualTo(1));
    }
    [Test]
    public async Task Reporting_declarations_are_listed_for_administrators_only_and_a_replay_does_not_duplicate_them()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);var approval=await w.Approved();
        w.Clock.Now=w.Clock.Now.AddSeconds(1);
        await w.Service.AssessAsync(new(w.School,w.Enrollment,w.FinalDefinition,8,w.Clock.Now,approval.DecisionId),Guid.NewGuid(),w.Instructor,default);
        var prepared=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.CompletionReport),Guid.NewGuid(),w.Admin,default)).Value;
        var operation=Guid.NewGuid();var request=new ReportingDeclarationRequest(prepared.SnapshotId,"Administrator reports manual handoff");
        var first=await w.Service.DeclareAsync(w.School,w.Enrollment,request,operation,w.Admin,default);
        var replay=await w.Service.DeclareAsync(w.School,w.Enrollment,request,operation,w.Admin,default);
        var listed=await w.Service.DeclarationsAsync(w.School,w.Enrollment,w.Admin,default);
        Assert.That(listed.Select(x=>x.DeclarationId),Is.EqualTo(new[]{first.Value.DeclarationId}));
        Assert.That(replay.Value.DeclarationId,Is.EqualTo(first.Value.DeclarationId));
        Assert.That(listed[0].Statement,Is.EqualTo("Administrator reports manual handoff"));
        Business.ThrowsBusinessAsync(async()=>await w.Service.DeclarationsAsync(w.School,w.Enrollment,w.Learner,default));
    }
    [Test]
    public async Task Missing_confirmations_make_journal_incomplete_and_stale_ready_is_not_current()
    {
        var w=new TestWorld();await w.Published();var practical=await w.Practical();
        var journal=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        Assert.That(journal.Status,Is.EqualTo(EvidenceStatus.Incomplete));Assert.That(journal.Checks.Any(x=>x.Outcome==CheckOutcome.Unsatisfied),Is.True);
        var a=(await w.Service.RespondAsync(w.School,practical.RecordId,practical.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),w.Instructor,default)).Value;
        var b=(await w.Service.RespondAsync(w.School,practical.RecordId,a.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),w.Learner,default)).Value;
        var ready=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;Assert.That(ready.Status,Is.EqualTo(EvidenceStatus.Ready));
        await w.Service.RespondAsync(w.School,b.RecordId,b.Revision,ResponseKind.Dispute,"Check original distance",Guid.NewGuid(),w.Learner,default);
        Assert.That((await w.Service.EvidenceAsync(w.School,ready.SnapshotId,w.Admin,default)).Current,Is.False);
    }
    [Test]
    public async Task Assessment_versions_keep_history_and_annulment_is_not_failure()
    {
        var w=new TestWorld();await w.Published();
        var first=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.MidtermDefinition,8,w.Clock.Now),Guid.NewGuid(),w.Instructor,default)).Value;
        var corrected=(await w.Service.CorrectAssessmentAsync(w.School,first.AssessmentId,new(first.Revision,9,ResultStatus.Recorded,"Corrected grade"),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(corrected.Version,Is.EqualTo(2));
        var annulled=(await w.Service.CorrectAssessmentAsync(w.School,first.AssessmentId,new(corrected.Revision,9,ResultStatus.Annulled,"Invalid assessment evidence"),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(annulled.Outcome,Is.EqualTo(Outcome.Passed));Assert.That(annulled.Status,Is.EqualTo(ResultStatus.Annulled));
        var current=await w.Service.AssessmentsAsync(w.School,w.Enrollment,w.Learner,default);Assert.That(current.Single().Version,Is.EqualTo(3));
        Assert.That(w.Memory.Rows.OfType<AssessmentResultVersion>().Count(),Is.EqualTo(3));
        var history=await w.Service.AssessmentHistoryAsync(w.School,first.AssessmentId,w.Learner,default);
        Assert.That(history,Has.Length.EqualTo(3));Assert.That(history.Count(x=>x.Current),Is.EqualTo(1));Assert.That(history[0].CorrectionReason,Is.Null);
    }
    [Test]
    public async Task Programme_missing_policy_or_association_cannot_award_credit()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);w.PolicyAvailable=false;
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);Assert.That(progress.PracticalMinutes,Is.Zero);Assert.That(progress.SourcesVerified,Is.False);
        var evidence=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;Assert.That(evidence.Status,Is.EqualTo(EvidenceStatus.Incomplete));
    }
    [Test]
    public async Task Prospective_programme_validation_does_not_require_existing_association()
    {
        var w=new TestWorld();await w.Published();w.AssociationAvailable=false;
        var result=await w.Service.ApplicabilityAsync(new(w.School,w.Enrollment,w.Group,w.ProgramVersion),default);Assert.That(result.Allowed,Is.True);
    }
    [Test]
    public async Task Planner_uses_versioned_session_policy_and_separates_existing_allocations_from_credit()
    {
        var w=new TestWorld();await w.Published();
        var input=new DraftInputs(w.School,w.ProgramVersion,[w.Enrollment],[],[w.Teacher],[w.Vehicle],new(2026,11,25),new(2026,11,25),Windows:[new(w.Clock.Now.AddDays(1),w.Clock.Now.AddDays(1).AddHours(2))]);
        var plan=await w.Service.PlanAsync(new(input,w.AdminUser),default);Assert.That(plan.Demands.Single().DurationMinutes,Is.EqualTo(60));Assert.That(plan.Demands.Single().Options.Length,Is.EqualTo(2));
        var repeat=await w.Service.PlanAsync(new(input,w.AdminUser),default);Assert.That(repeat.Version,Is.EqualTo(plan.Version));
        w.Allocated=[new(w.PracticalLesson,TrainingKind.Practical,w.Enrollment,null,60,"Scheduled")];
        var covered=await w.Service.PlanAsync(new(input,w.AdminUser),default);Assert.That(covered.Demands,Is.Empty);
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
        var groupPlan=await w.Service.PlanAsync(new(input with{EnrollmentIds=[],GroupIds=[w.Group]},w.AdminUser),default);Assert.That(groupPlan.Demands.Single().Kind,Is.EqualTo(TrainingKind.Theory));
    }
    [Test]
    public async Task Integration_duplicate_out_of_order_and_malformed_old_sources_never_recredit()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);
        var event1=new IntegrationMessage(Guid.NewGuid(),"LessonCompleted",1,"scheduling",w.School,w.PracticalLesson,1,w.Clock.Now,"event-1","{}");
        await w.Service.ConsumeAsync(event1,default);await w.Service.ConsumeAsync(event1,default);
        Assert.That(w.Memory.Rows.OfType<InboxMessage>().Count(),Is.EqualTo(1));
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2,PhysicallyCompleted=false,SourceValid=false,ActualStart=null,ActualEnd=null};
        await w.Service.ConsumeAsync(event1 with{EventId=Guid.NewGuid(),AggregateVersion=2},default);
        var revision=w.Memory.Rows.OfType<StudentTraining>().Single().DependencyRevision;
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=1,ActualStart=w.Clock.Now,ActualEnd=null};
        await w.Service.ConsumeAsync(event1 with{EventId=Guid.NewGuid()},default);
        Assert.That(w.Memory.Rows.OfType<StudentTraining>().Single().DependencyRevision,Is.EqualTo(revision));
        Assert.That(w.Memory.Rows.OfType<LessonSourceState>().Single().Freshness,Is.EqualTo(Freshness.Current));
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
    }
    [Test]
    public async Task School_events_and_reconciliation_keep_external_enrollment_ownership()
    {
        var w=new TestWorld();await w.Published();
        var message=new IntegrationMessage(Guid.NewGuid(),"StudentEnrollmentStarted",1,"school",w.School,w.Enrollment,1,w.Clock.Now,"enrollment-1","{}");
        await w.Service.ConsumeAsync(message,default);w.EnrollmentRevision=2;w.EnrollmentStatus="Withdrawn";
        await w.Service.ConsumeAsync(message with{EventId=Guid.NewGuid(),EventType="StudentEnrollmentEnded",AggregateVersion=2},default);
        await w.Service.ReconcileEnrollmentAsync(w.School,w.Enrollment,default);
        await w.Service.ConsumeAsync(message with{EventId=Guid.NewGuid(),EventType="SchoolLearningAuthorityChanged",AggregateId=w.School},default);
        Assert.That(w.Memory.Rows.OfType<EnrollmentSourceState>().Single().Status,Is.EqualTo("Withdrawn"));
        Assert.That(w.Memory.Rows.OfType<StudentTraining>().Single().Id,Is.EqualTo(w.Enrollment));
        Business.ThrowsBusinessAsync(async()=>await w.Service.StartAsync(new(w.School,w.Enrollment,w.ProgramVersion),Guid.NewGuid(),w.Admin,default));
    }
}

