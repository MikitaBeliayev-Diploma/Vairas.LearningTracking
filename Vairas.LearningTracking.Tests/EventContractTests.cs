using System.Text.Json;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using LearningSender=Vairas.LearningTracking.Integrations.ServiceBusSender;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Unit")]
public sealed class EventContractTests
{
    [Test]
    public async Task Adopted_confirmation_revision_and_result_payload_shapes_are_retained()
    {
        var w=new TestWorld();await w.Published();var practical=await w.Practical(true);await w.Theory(true);var approval=await w.Approved();w.Clock.Now=w.Clock.Now.AddSeconds(1);
        var assessment=(await w.Service.AssessAsync(new(w.School,w.Enrollment,w.FinalDefinition,8,w.Clock.Now,approval.DecisionId),Guid.NewGuid(),w.Instructor,default)).Value;
        await w.Service.CorrectAssessmentAsync(w.School,assessment.AssessmentId,new(assessment.Revision,9,ResultStatus.Recorded,"Corrected recorded grade"),Guid.NewGuid(),w.Instructor,default);
        await w.Service.RespondAsync(w.School,practical.RecordId,practical.Revision,ResponseKind.Dispute,"Distance needs review",Guid.NewGuid(),w.Learner,default);
        await w.Service.AssessAsync(new(w.School,w.Enrollment,w.MidtermDefinition,3,w.Clock.Now),Guid.NewGuid(),w.Instructor,default);
        var events=w.Memory.Rows.OfType<OutboxMessage>().ToArray();
        var confirmed=JsonDocument.Parse(events.Single(x=>x.EventType=="PracticalRecordConfirmed").Payload).RootElement;
        Assert.That(confirmed.GetProperty("recordRevision").GetInt64(),Is.EqualTo(3));Assert.That(confirmed.GetProperty("creditedDurationMinutes").GetInt32(),Is.EqualTo(60));Assert.That(confirmed.GetProperty("lessonId").GetGuid(),Is.EqualTo(w.PracticalLesson));
        var revised=JsonDocument.Parse(events.Single(x=>x.EventType=="PracticalRecordRevised").Payload).RootElement;
        Assert.That(revised.GetProperty("previousRevision").GetInt64(),Is.EqualTo(3));Assert.That(revised.GetProperty("recordRevision").GetInt64(),Is.EqualTo(4));
        var samples=events.Select(e=>new{eventType=e.EventType,headers=LearningSender.ToMessage(e).ApplicationProperties,payload=JsonDocument.Parse(e.Payload).RootElement.Clone()}).ToArray();
        Directory.CreateDirectory(Path.Combine(PostgresFixture.Root,"evidence"));
        await File.WriteAllTextAsync(Path.Combine(PostgresFixture.Root,"evidence/learning-event-samples.json"),JsonSerializer.Serialize(new{origin="executed synthetic business-handler unit tests",events=samples},Wire.Json));
    }
    [Test]
    public async Task Delayed_School_revision_does_not_withdraw_a_newer_verified_decision()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);w.SchoolRevision=2;
        await w.Service.EvaluateAsync(w.School,w.Enrollment,new(w.FinalDefinition),Guid.NewGuid(),w.Admin,default);
        var approved=await w.Approved();var dependency=w.Memory.Rows.OfType<StudentTraining>().Single().DependencyRevision;
        await w.Service.ConsumeAsync(new(Guid.NewGuid(),"SchoolLearningAuthorityChanged",1,"school",w.School,w.School,1,w.Clock.Now.AddSeconds(-1),"delayed","{}"),default);
        Assert.That(w.Memory.Rows.OfType<StudentTraining>().Single().DependencyRevision,Is.EqualTo(dependency));Assert.That(w.Memory.Rows.OfType<ReadinessDecision>().Single(x=>x.Id==approved.DecisionId).InvalidatedAt,Is.Null);
    }
}
