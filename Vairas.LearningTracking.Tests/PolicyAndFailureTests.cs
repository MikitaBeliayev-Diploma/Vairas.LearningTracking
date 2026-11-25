using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Unit")]
public sealed class PolicyAndFailureTests
{
    [Test]
    public async Task Unassigned_training_has_no_invented_denominator_or_deficit()
    {
        var w=new TestWorld();await w.Service.ReconcileEnrollmentAsync(w.School,w.Enrollment,default);
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);
        Assert.That(progress.State,Is.EqualTo("AwaitingProgramme"));Assert.That(progress.Percent,Is.Null);Assert.That(progress.TheoryDeficitMinutes,Is.Null);Assert.That(progress.PracticalMinutes,Is.Zero);
    }
    [Test]
    public void Synthetic_checklist_requires_explicit_mode_and_exact_scope()
    {
        var w=new TestWorld();var registry=new ChecklistRegistry(new(){Policies=[w.Policy]},w.Clock);
        Assert.That(registry.Find(w.ChecklistId,w.School,"B","synthetic",new(2026,11,24)),Is.Null);
        registry=new(new(){Policies=[w.Policy],SyntheticMode=true},w.Clock);
        Assert.That(registry.Find(w.ChecklistId,w.School,"B","synthetic",new(2026,11,24)),Is.Not.Null);
        Assert.That(registry.Find(w.ChecklistId,Guid.NewGuid(),"B","synthetic",new(2026,11,24)),Is.Null);
        Assert.Throws<InvalidOperationException>(()=>new ChecklistRegistry(new(){Policies=[w.Policy,w.Policy]},w.Clock));
    }
    [Test]
    public async Task Publication_rejects_cycles_and_incomplete_rule_provenance()
    {
        var w=new TestWorld();var p=(await w.Service.CreateProgramAsync(new(w.School,"B","Synthetic"),Guid.NewGuid(),w.Admin,default)).Value;
        var r=w.Curriculum();var definitions=r.Assessments;
        definitions[0]=definitions[0] with{Requirements=[new(Guid.NewGuid(),"needs-final",RequirementKind.AssessmentPassed,PrerequisiteDefinitionVersionId:w.FinalDefinition)]};
        definitions[1]=definitions[1] with{Requirements=[new(Guid.NewGuid(),"needs-midterm",RequirementKind.MinimumMark,PrerequisiteDefinitionVersionId:w.MidtermDefinition,MinimumMark:7)]};
        var draft=(await w.Service.SaveVersionAsync(p.ProgramId,null,r,Guid.NewGuid(),w.Admin,default)).Value;
        var cycle=Assert.ThrowsAsync<RuleViolation>(async()=>await w.Service.LifecycleAsync(w.School,draft.ProgramVersionId,"publish",new(draft.Revision),Guid.NewGuid(),w.Admin,default));Assert.That(cycle!.Code,Is.EqualTo("CYCLIC_PREREQUISITES"));
        var edited=(await w.Service.SaveVersionAsync(p.ProgramId,draft.ProgramVersionId,r with{ExpectedRevision=draft.Revision,Sources=r.Sources.Select(x=>x with{Verified=false}).ToArray()},Guid.NewGuid(),w.Admin,default)).Value;
        Assert.ThrowsAsync<RuleViolation>(async()=>await w.Service.LifecycleAsync(w.School,draft.ProgramVersionId,"publish",new(edited.Revision),Guid.NewGuid(),w.Admin,default));
    }
    [Test]
    public async Task Failed_required_assessment_and_topic_marks_do_not_satisfy_overall_prerequisite()
    {
        var w=new TestWorld();var p=(await w.Service.CreateProgramAsync(new(w.School,"B","Synthetic"),Guid.NewGuid(),w.Admin,default)).Value;w.Program=p.ProgramId;
        var r=w.Curriculum(theory:0,practical:0);r.Assessments[1]=r.Assessments[1] with{Requirements=[new(Guid.NewGuid(),"prior-pass",RequirementKind.AssessmentPassed,PrerequisiteDefinitionVersionId:w.MidtermDefinition),new(Guid.NewGuid(),"prior-mark",RequirementKind.MinimumMark,PrerequisiteDefinitionVersionId:w.MidtermDefinition,MinimumMark:7)]};
        var v=(await w.Service.SaveVersionAsync(w.Program,null,r,Guid.NewGuid(),w.Admin,default)).Value;w.ProgramVersion=v.ProgramVersionId;
        await w.Service.LifecycleAsync(w.School,v.ProgramVersionId,"publish",new(v.Revision),Guid.NewGuid(),w.Admin,default);await w.Service.StartAsync(new(w.School,w.Enrollment,w.ProgramVersion),Guid.NewGuid(),w.Admin,default);
        var topic=w.Memory.Rows.OfType<AssessmentTopic>().Single(x=>x.DefinitionVersionId==w.MidtermDefinition);
        var failed=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.MidtermDefinition,3,w.Clock.Now,TopicMarks:[new(topic.Id,10,"Topic passed but overall failed")]),Guid.NewGuid(),w.Instructor,default)).Value;Assert.That(failed.Outcome,Is.EqualTo(Outcome.Failed));
        var blocked=(await w.Service.EvaluateAsync(w.School,w.Enrollment,new(w.FinalDefinition),Guid.NewGuid(),w.Learner,default)).Value;Assert.That(blocked.FormalStatus,Is.EqualTo(FormalStatus.Incomplete));
        await w.Service.CorrectAssessmentAsync(w.School,failed.AssessmentId,new(failed.Revision,8,ResultStatus.Recorded,"Corrected recorded overall mark",TopicMarks:[new(topic.Id,9)]),Guid.NewGuid(),w.Instructor,default);
        var approval=await w.Approved();Assert.That(approval.Status,Is.EqualTo("Approved"));
        var evidence=await w.Service.AssessmentEvidenceAsync(new(new(w.School,w.Enrollment,w.FinalDefinition,approval.EvaluationId,w.Teacher,w.Branch,w.Clock.Now.AddDays(1),w.Clock.Now.AddDays(1).AddHours(1),"Synthetic",w.Vehicle),w.AdminUser),default);Assert.That(evidence.Allowed,Is.True);
    }
    [Test]
    public async Task Repeated_assessment_is_applicable_only_while_the_requirement_is_outstanding()
    {
        // US-010 / AC-024..025: a failed attempt keeps the requirement outstanding; a passed one makes a repeat inapplicable.
        var w=new TestWorld();await w.Published();
        AssessmentEvidenceRequest Request() => new(new(w.School,w.Enrollment,w.MidtermDefinition,Guid.NewGuid(),w.Teacher,w.Branch,w.Clock.Now.AddDays(1),w.Clock.Now.AddDays(1).AddHours(1),"Repeated assessment",w.Vehicle),w.AdminUser);
        var failed=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.MidtermDefinition,3,w.Clock.Now),Guid.NewGuid(),w.Instructor,default)).Value;
        Assert.That(failed.Outcome,Is.EqualTo(Outcome.Failed));
        Assert.That((await w.Service.AssessmentEvidenceAsync(Request(),default)).Allowed,Is.True,"a failed attempt leaves the requirement outstanding");
        w.Clock.Now=w.Clock.Now.AddMinutes(1);
        await w.Service.AssessAsync(new(w.School,w.Enrollment,w.MidtermDefinition,9,w.Clock.Now),Guid.NewGuid(),w.Instructor,default);
        var evidence=await w.Service.AssessmentEvidenceAsync(Request(),default);
        Assert.That(evidence.Allowed,Is.False,"a passed attempt makes a repeated assessment inapplicable");
        Assert.That(evidence.EnrollmentId,Is.EqualTo(w.Enrollment));
    }
    [Test]
    public async Task Additional_lesson_applicability_follows_outstanding_minutes_and_required_assessments_of_its_kind()
    {
        // US-010 / AC-024..025: Scheduling asks Learning before linking an additional lesson to an enrollment.
        var w=new TestWorld();await w.Published();
        Task<TrainingRequirementEvidence> Ask(TrainingKind kind)=>w.Service.RequirementAsync(new(w.School,w.Enrollment,kind,w.AdminUser),default);
        Assert.That((await Ask(TrainingKind.Practical)).Outstanding,Is.True,"no practical minutes credited yet");
        await w.Practical(true);await w.Theory(true);
        Assert.That((await Ask(TrainingKind.Theory)).Outstanding,Is.False,"theory minutes credited and no required theory assessment");
        Assert.That((await Ask(TrainingKind.Practical)).Outstanding,Is.True,"minutes credited but the required practical final is not passed");
        var approval=await w.Approved();w.Clock.Now=w.Clock.Now.AddSeconds(1);
        await w.Service.AssessAsync(new(w.School,w.Enrollment,w.FinalDefinition,8,w.Clock.Now,approval.DecisionId),Guid.NewGuid(),w.Instructor,default);
        var done=await Ask(TrainingKind.Practical);
        Assert.That(done.Outstanding,Is.False,"minutes credited and the required practical final passed");
        Assert.That((done.SchoolId,done.EnrollmentId,done.Kind),Is.EqualTo((w.School,w.Enrollment,TrainingKind.Practical)));
        Business.ThrowsBusinessAsync(async()=>await w.Service.RequirementAsync(new(w.School,w.Enrollment,TrainingKind.Practical,w.StudentUser),default));
    }
    [Test]
    public async Task Same_revision_changed_source_is_held_and_cannot_keep_ready_evidence()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{ActualEnd=w.Clock.Now};
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Admin,default);Assert.That(progress.SourcesVerified,Is.False);
        Assert.That(w.Memory.Rows.OfType<LessonSourceSnapshot>().Count(),Is.EqualTo(1));
        Business.ThrowsBusinessAsync(async()=>await w.Service.ConfirmedAsync(w.School,w.Enrollment,default));
    }
    [Test]
    public async Task Event_identity_reuse_and_unknown_version_are_rejected()
    {
        var w=new TestWorld();await w.Published();
        var m=new IntegrationMessage(Guid.NewGuid(),"LessonCompleted",1,"scheduling",w.School,w.PracticalLesson,1,w.Clock.Now,"test","{}");await w.Service.ConsumeAsync(m,default);
        Business.ThrowsBusinessAsync(async()=>await w.Service.ConsumeAsync(m with{Payload="{\"changed\":true}"},default));
        Business.ThrowsBusinessAsync(async()=>await w.Service.ConsumeAsync(m with{EventId=Guid.NewGuid(),EventVersion=2},default));
        await w.Service.ReconcileLessonAsync(w.School,w.PracticalLesson,default);
    }
    [Test]
    public async Task Policy_unknown_at_evaluation_is_unverified_and_never_approved()
    {
        var w=new TestWorld();await w.Published();w.PolicyAvailable=false;
        var evaluation=(await w.Service.EvaluateAsync(w.School,w.Enrollment,new(w.FinalDefinition),Guid.NewGuid(),w.Admin,default)).Value;Assert.That(evaluation.FormalStatus,Is.EqualTo(FormalStatus.Unverified));
        w.GrantAssessor=false;
        Business.ThrowsBusinessAsync(async()=>await w.Service.DecideAsync(w.School,w.Enrollment,new(evaluation.EvaluationId,Decision.Approved,"Not permitted",evaluation.Revision),Guid.NewGuid(),w.Instructor,default));
    }
}
