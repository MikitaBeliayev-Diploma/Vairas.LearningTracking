using NUnit.Framework;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Unit")]
public sealed class DomainTests
{
    [TestCase(0,false,0)]
    [TestCase(60,false,1)]
    [TestCase(120,false,2)]
    [TestCase(75,true,2)]
    public void Planning_policy_splits_exact_demand(int minutes,bool allowShort,int count)
    {Assert.That(LearningRules.SplitDemand(minutes,new(){LessonMinutes=60,MinimumFinalLessonMinutes=15,AllowShortFinalLesson=allowShort}),Has.Length.EqualTo(count));}
    [TestCase(65,true)]
    [TestCase(75,false)]
    [TestCase(-1,true)]
    public void Unplannable_remainders_are_explained_instead_of_rounded_up(int minutes,bool allowShort)
    {Assert.Throws<RuleViolation>(()=>LearningRules.SplitDemand(minutes,new(){LessonMinutes=60,MinimumFinalLessonMinutes=15,AllowShortFinalLesson=allowShort}));}
    [TestCase(0,0,0,0,null)]
    [TestCase(20,50,60,60,58.33)]
    [TestCase(180,0,60,60,50)]
    public void Percent_is_bounded_and_zero_denominator_is_not_applicable(int theory,int practical,int requiredTheory,int requiredPractical,double? expected)
    {Assert.That(LearningRules.Percent(theory,practical,requiredTheory,requiredPractical),Is.EqualTo(expected is null?null:(decimal?)expected));}
    [Test]
    public void Physical_completion_without_actuals_and_half_intervals_are_invalid()
    {
        var w=new TestWorld();var s=new LessonSourceSnapshot{SchoolId=w.School,LessonId=w.PracticalLesson,LessonRevision=1,Kind=TrainingKind.Practical,InstructorId=w.Teacher,EnrollmentId=w.Enrollment,StudentId=w.Student,VehicleId=w.Vehicle,PhysicallyCompleted=true,SourceValid=true};
        Assert.That(LearningRules.SourceShape(s),Is.False);
        s.ActualStart=w.Clock.Now;Assert.That(LearningRules.SourceShape(s),Is.False);
        s.ActualEnd=w.Clock.Now.AddHours(1);Assert.That(LearningRules.SourceShape(s),Is.True);
        Assert.Throws<RuleViolation>(()=>LearningRules.TeachingMinutes(s,61,null));
        Assert.Throws<RuleViolation>(()=>LearningRules.TeachingMinutes(s,50,null));
        Assert.DoesNotThrow(()=>LearningRules.TeachingMinutes(s,50,"Ten-minute break"));
        s.PhysicallyCompleted=false;s.SourceValid=false;s.ActualStart=null;s.ActualEnd=null;Assert.That(LearningRules.SourceShape(s),Is.True);
    }
    [TestCase(InvalidationScope.ProspectiveOnly,1,ResultStatus.Recorded)]
    [TestCase(InvalidationScope.ProspectiveOnly,-1,ResultStatus.ReviewRequired)]
    [TestCase(InvalidationScope.HistoricalReviewRequired,0,ResultStatus.ReviewRequired)]
    public void Historical_assessment_validity_depends_on_effective_time(InvalidationScope scope,int effectiveDay,ResultStatus expected)
    {
        var now=DateTimeOffset.UtcNow;
        var decision=new ReadinessDecision{Id=Guid.NewGuid(),Decision=Decision.Approved,DecidedAt=now.AddDays(-2),InvalidatedAt=now.AddDays(2),InvalidationScope=scope,InvalidationEffectiveAt=effectiveDay==0?null:now.AddDays(effectiveDay)};
        var result=new AssessmentResultVersion{Status=ResultStatus.Recorded,ReadinessDecisionId=decision.Id,AssessedAt=now};
        Assert.That(LearningRules.HistoricalStatus(result,decision),Is.EqualTo(expected));
        if(scope==InvalidationScope.HistoricalReviewRequired){result.ReviewedInvalidationAt=decision.InvalidatedAt;Assert.That(LearningRules.HistoricalStatus(result,decision),Is.EqualTo(ResultStatus.Recorded));}
        decision.InvalidatedAt=now.AddMinutes(-1);Assert.That(LearningRules.HistoricalStatus(result,decision),Is.EqualTo(ResultStatus.ReviewRequired));
        Assert.That(LearningRules.HistoricalStatus(result,null),Is.EqualTo(ResultStatus.ReviewRequired));
        result.Status=ResultStatus.Annulled;Assert.That(LearningRules.HistoricalStatus(result,decision),Is.EqualTo(ResultStatus.Annulled));
    }
    [Test]
    public void Same_source_compares_typed_material_fields_and_not_transport_identity()
    {
        var id=Guid.NewGuid();var school=Guid.NewGuid();var a=new LessonSourceSnapshot{SchoolId=school,LessonId=id,LessonRevision=1};
        var b=new LessonSourceSnapshot{SchoolId=school,LessonId=id,LessonRevision=1,Id=Guid.NewGuid(),CapturedAt=DateTimeOffset.UtcNow};
        Assert.That(LearningRules.SameSource(a,b),Is.True);b.LessonRevision=2;Assert.That(LearningRules.SameSource(a,b),Is.False);
    }
}
