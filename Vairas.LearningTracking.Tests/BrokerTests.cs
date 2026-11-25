using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Integrations;
using Vairas.LearningTracking.Persistance;
using LearningSender=Vairas.LearningTracking.Integrations.ServiceBusSender;
using Vairas.LearningTracking.Configuration.Options;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Broker"),NonParallelizable]
public sealed class BrokerTests
{
    [Test]
    public async Task Azure_SDK_delivery_duplicates_ordering_poison_DLQ_and_outbox_are_real_broker_operations()
    {
        var connection=Environment.GetEnvironmentVariable("VAIRAS_TEST_SERVICEBUS_CONNECTION");
        Assert.That(connection,Is.Not.Null.And.Not.Empty,"Run scripts/test-broker.ps1 with a locally configured emulator.");
        await using var pg=new PostgresFixture();await pg.Start();
        var w=new TestWorld{Store=pg.Store};w.Clock.Now=DateTimeOffset.UtcNow;await w.Published();await w.Practical(true);
        var options=new ServiceBusOptions{Enabled=true,ConnectionString=connection!,MaxDeliveryCount=3};
        await using var services=new ServiceCollection().AddScoped<ILearningIntegrationService>(_=>w.Service).BuildServiceProvider();
        using var worker=new InboxWorker(services.GetRequiredService<IServiceScopeFactory>(),Microsoft.Extensions.Options.Options.Create(options),NullLogger<InboxWorker>.Instance);
        await worker.StartAsync(default);
        await using var client=new ServiceBusClient(connection);
        await using var sender=client.CreateSender(options.SchedulingTopic);
        var original=new OutboxMessage{Id=Guid.NewGuid(),SchoolId=w.School,AggregateId=w.PracticalLesson,AggregateVersion=1,EventType="LessonCompleted",Payload=Wire.Save(new{schoolId=w.School,lessonId=w.PracticalLesson}),CorrelationId="broker-test",OccurredAt=w.Clock.Now};
        ServiceBusMessage Incoming(OutboxMessage message)
        {var m=LearningSender.ToMessage(message);m.ApplicationProperties["producer"]="scheduling";return m;}
        try
        {
            await sender.SendMessageAsync(Incoming(original));await sender.SendMessageAsync(Incoming(original));
            await Wait(async()=>{await using var db=pg.OwnerDb();return await db.Set<InboxMessage>().CountAsync(x=>x.Id==original.Id)==1;});
            w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2,ActualEnd=w.Sources[w.PracticalLesson].ActualEnd!.Value.AddMinutes(-1)};
            var corrected=new OutboxMessage{Id=Guid.NewGuid(),SchoolId=w.School,AggregateId=w.PracticalLesson,AggregateVersion=2,EventType="LessonCompleted",Payload=original.Payload,CorrelationId="broker-test",OccurredAt=w.Clock.Now};
            await sender.SendMessageAsync(Incoming(corrected));
            await Wait(async()=>{await using var db=pg.OwnerDb();return await db.Set<LessonSourceSnapshot>().AnyAsync(x=>x.SchoolId==w.School && x.LessonRevision==2);});
            var late=Incoming(original);late.MessageId=Guid.NewGuid().ToString("D");late.ApplicationProperties["eventId"]=late.MessageId;
            await sender.SendMessageAsync(late);
            var poison=Incoming(original);poison.MessageId=Guid.NewGuid().ToString("D");poison.ApplicationProperties["eventId"]=poison.MessageId;poison.Subject="UnknownEvent";
            await sender.SendMessageAsync(poison);
            await using var dead=client.CreateReceiver(options.SchedulingTopic,options.SubscriptionName,new(){SubQueue=SubQueue.DeadLetter});
            ServiceBusReceivedMessage? rejected=null;
            await Wait(async()=>{rejected=await dead.PeekMessageAsync();return rejected is not null;});
            Assert.That(rejected!.DeadLetterReason,Is.EqualTo("LearningProcessingFailed"));
            await using(var db=pg.OwnerDb())
            {Assert.That(await db.Set<LessonSourceSnapshot>().CountAsync(x=>x.SchoolId==w.School),Is.EqualTo(2));Assert.That(await db.Set<PracticalRecordResponse>().CountAsync(x=>x.SchoolId==w.School),Is.EqualTo(2));}
            await PostgresFixture.Sql(pg.Owner,$"UPDATE outbox_message SET next_attempt_at=clock_timestamp()-interval '1 second' WHERE school_id='{w.School}'");
            var store=new OutboxStore(pg.Delivery);var outgoing=await store.ClaimAsync(default);Assert.That(outgoing,Is.Not.Null);
            await using var publisher=new LearningSender(Microsoft.Extensions.Options.Options.Create(options));await publisher.SendAsync(outgoing!,default);await store.FinishAsync(outgoing!.Id,outgoing.LeaseId!.Value,true,default);
            await using var receiver=client.CreateReceiver(options.TopicName,"assertions");var received=await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(20));Assert.That(received,Is.Not.Null);
            Assert.That(received!.MessageId,Is.EqualTo(outgoing.Id.ToString("D")));Assert.That(received.ApplicationProperties["producer"],Is.EqualTo("learning"));await receiver.CompleteMessageAsync(received);
        }
        finally {await worker.StopAsync(default);}
    }
    private static async Task Wait(Func<Task<bool>> predicate)
    {
        var until=DateTimeOffset.UtcNow.AddSeconds(30);while(DateTimeOffset.UtcNow<until){if(await predicate())return;await Task.Delay(200);}Assert.Fail("Broker processing condition was not reached.");
    }
}
