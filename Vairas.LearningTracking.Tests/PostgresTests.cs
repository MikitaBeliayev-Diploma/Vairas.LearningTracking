using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using NUnit.Framework;
using Testcontainers.PostgreSql;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
using Vairas.LearningTracking.Persistance;
namespace Vairas.LearningTracking.Tests;
[TestFixture,Category("Integration"),NonParallelizable]
public sealed class PostgresTests
{
    private PostgresFixture pg=null!;
    [OneTimeSetUp]public async Task Start(){pg=new();await pg.Start();}
    [OneTimeTearDown]public async Task Stop(){if(pg is not null)await pg.DisposeAsync();}
    private TestWorld World()=>new(){Store=pg.Store};
    [Test]
    public async Task Same_person_in_two_schools_keeps_programme_and_confirmed_credit_enrollment_specific()
    {
        var first = World();
        var second = new TestWorld(first.Student, first.StudentUser) { Store = pg.Store };
        await first.Published(); await second.Published();
        await first.Practical(true);
        var a = await first.Service.ProgressAsync(first.School, first.Enrollment, first.Learner, default);
        var b = await second.Service.ProgressAsync(second.School, second.Enrollment, second.Learner, default);
        Assert.Multiple(() =>
        {
            Assert.That(a.EnrollmentId, Is.EqualTo(first.Enrollment));
            Assert.That(b.EnrollmentId, Is.EqualTo(second.Enrollment));
            Assert.That(a.PracticalMinutes, Is.EqualTo(60));
            Assert.That(b.PracticalMinutes, Is.Zero);
            Assert.That(b.PracticalDeficitMinutes, Is.EqualTo(60));
            Assert.That(first.ProgramVersion, Is.Not.EqualTo(second.ProgramVersion));
        });
        var denied = Business.ThrowsBusinessAsync(async () => await first.Service.ProgressAsync(first.School, second.Enrollment, first.Learner, default));
        Assert.That(denied!.HttpStatus(), Is.EqualTo(404));
        var unchanged = await first.Service.ProgressAsync(first.School, first.Enrollment, first.Learner, default);
        Assert.That(unchanged.PracticalMinutes, Is.EqualTo(60));
    }
    [Test]
    public async Task Concurrent_document_release_and_practical_correction_keep_a_historical_decision_and_deny_new_current_use()
    {
        var w=World();await w.Published();var practical=await w.Practical(true);await w.Theory(true);
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        var operation=Guid.NewGuid();var request=new DocumentReleaseRequest(w.School,w.Enrollment,snapshot.SnapshotId,snapshot.DependencyRevision,Guid.NewGuid(),w.AdminUser,"PracticalJournal",new string('a',64),"Publication");
        var release=w.Service.DecideDocumentAsync(operation,request,default);
        var correction=w.Service.PracticalAsync(practical.RecordId,practical.Facts with{ExpectedRevision=practical.Revision,DistanceKm=22,CorrectionReason="Concurrent factual correction."},Guid.NewGuid(),w.Instructor,default);
        await Task.WhenAll(release,correction);var result=await release;
        Assert.That(result.Status,Is.AnyOf("Accepted","Rejected"));
        if(result.Status=="Accepted")Assert.That(result.EvaluatedRevision,Is.EqualTo(snapshot.DependencyRevision));
        else Assert.That(result.EvaluatedRevision,Is.GreaterThan(snapshot.DependencyRevision));
        Assert.That((await w.Service.DecideDocumentAsync(Guid.NewGuid(),request with{Purpose="CurrentUse"},default)).Status,Is.EqualTo("Rejected"));
        Assert.That(await w.Service.DocumentDecisionAsync(w.School,operation,w.AdminUser,default),Is.EqualTo(result));
        await using var s=await pg.Store.OpenAsync(w.School,false,default);Assert.That((await s.ListAsync<DocumentReleaseDecision>(x=>true,default)).Count,Is.EqualTo(2));
    }
    [Test]
    public async Task Canonical_schema_migrates_and_runtime_has_no_unscoped_access()
    {
        await using var db=pg.OwnerDb();await db.Database.MigrateAsync();
        Assert.That((await db.Database.GetAppliedMigrationsAsync()).Count(),Is.EqualTo(5));
        Assert.That(await PostgresFixture.Scalar(pg.Runtime,"SELECT count(*) FROM training_program"),Is.EqualTo(0L));
        Assert.ThrowsAsync<PostgresException>(async()=>await PostgresFixture.Sql(pg.Runtime,"CREATE TABLE forbidden(id integer)"));
        Assert.ThrowsAsync<PostgresException>(async()=>await PostgresFixture.Sql(pg.Delivery,"SELECT * FROM practical_record_version"));
        Assert.That(await PostgresFixture.Scalar(pg.Owner,"SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename<>'__EFMigrationsHistory'"),Is.EqualTo(39L));
    }
    [Test]
    public async Task Real_database_practical_and_theory_credit_are_derived_from_confirmed_current_facts()
    {
        var w=World();await w.Published();await w.Practical(true);await w.Theory(true);
        var progress=await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default);
        Assert.That(progress.PracticalMinutes,Is.EqualTo(60));Assert.That(progress.TheoryMinutes,Is.EqualTo(60));Assert.That(progress.Percent,Is.EqualTo(100));
        await using var db=pg.OwnerDb();Assert.That(await db.Set<PracticalRecordResponse>().CountAsync(x=>x.SchoolId==w.School),Is.EqualTo(2));
    }
    [Test]
    public async Task PostgreSQL_concurrent_identical_commands_keep_one_result_and_one_outbox_intent_set()
    {
        var w=World();var key=Guid.NewGuid();var request=new CreateProgramRequest(w.School,"B","Concurrent");
        var results=await Task.WhenAll(Enumerable.Range(0,6).Select(_=>w.Service.CreateProgramAsync(request,key,w.Admin,default)));
        Assert.That(results.Select(x=>x.Value.ProgramId).Distinct().Count(),Is.EqualTo(1));
        Assert.That(results.Count(x=>!x.Replayed),Is.EqualTo(1));
        await using var db=pg.OwnerDb();Assert.That(await db.Set<LearningOperation>().CountAsync(x=>x.SchoolId==w.School),Is.EqualTo(1));
    }
    [Test]
    public async Task PostgreSQL_races_between_signers_preserve_one_current_CAS_revision()
    {
        var w=World();await w.Published();var record=await w.Practical();
        async Task<bool> Sign(LearningActor actor)
        {
            try{await w.Service.RespondAsync(w.School,record.RecordId,record.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),actor,default);return true;}
            catch(ConflictException){return false;}
        }
        var results=await Task.WhenAll(Sign(w.Instructor),Sign(w.Learner));Assert.That(results.Count(x=>x),Is.EqualTo(1));
        Assert.That((await w.Service.ProgressAsync(w.School,w.Enrollment,w.Learner,default)).PracticalMinutes,Is.Zero);
    }
    [Test]
    public async Task Runtime_cannot_rewrite_published_curriculum_facts_or_terminal_invalidation()
    {
        var w=World();await w.Published();await w.Practical(true);await w.Theory(true);var approval=await w.Approved();
        async Task Scoped(string sql)=>await PostgresFixture.Sql(pg.Runtime,$"BEGIN;SELECT set_config('vairas.school_id','{w.School}',true);{sql};COMMIT");
        Assert.ThrowsAsync<PostgresException>(async()=>await Scoped($"UPDATE training_program_version SET required_practical_minutes=0 WHERE id='{w.ProgramVersion}'"));
        Assert.ThrowsAsync<PostgresException>(async()=>await Scoped($"UPDATE practical_record_version SET training_minutes=1 WHERE school_id='{w.School}'"));
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2,ActualEnd=w.Sources[w.PracticalLesson].ActualEnd!.Value.AddMinutes(-1)};
        await w.Service.ProgressAsync(w.School,w.Enrollment,w.Admin,default);
        Assert.ThrowsAsync<PostgresException>(async()=>await Scoped($"UPDATE readiness_decision SET invalidated_at=NULL,invalidated_by=NULL,invalidation_reason=NULL,invalidation_dependency_revision=NULL,invalidation_scope=NULL,invalidation_effective_at=NULL WHERE id='{approval.DecisionId}'"));
    }
    [Test]
    public async Task Ready_evidence_requires_relational_checks_and_release_is_revalidated()
    {
        var w=World();await w.Published();await w.Practical(true);
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        Assert.That(snapshot.Status,Is.EqualTo(EvidenceStatus.Ready));
        await using var db=pg.OwnerDb();Assert.That(await db.Set<TrainingEvidenceCheck>().CountAsync(x=>x.SnapshotId==snapshot.SnapshotId),Is.GreaterThan(5));
        Assert.ThrowsAsync<PostgresException>(async()=>await PostgresFixture.Sql(pg.Runtime,$"BEGIN;SELECT set_config('vairas.school_id','{w.School}',true);INSERT INTO training_evidence_snapshot(id,school_id,enrollment_id,program_version_id,kind,status,checklist_version_id,dependency_revision,prepared_by,prepared_at,source_verified_at) VALUES('{Guid.NewGuid()}','{w.School}','{w.Enrollment}','{w.ProgramVersion}','Journal','Ready','{w.ChecklistId}',1,'{w.AdminUser}',clock_timestamp(),clock_timestamp());COMMIT"));
    }
    [Test]
    public async Task Inbox_and_source_change_commit_together_and_replay_does_not_duplicate_audit()
    {
        var w=World();await w.Published();
        var message=new IntegrationMessage(Guid.NewGuid(),"LessonCompleted",1,"scheduling",w.School,w.PracticalLesson,1,w.Clock.Now,"synthetic-event","{}");
        await w.Service.ConsumeAsync(message,default);await w.Service.ConsumeAsync(message,default);
        await using var db=pg.OwnerDb();Assert.That(await db.Set<InboxMessage>().CountAsync(x=>x.Id==message.EventId),Is.EqualTo(1));Assert.That(await db.Set<LessonSourceSnapshot>().CountAsync(x=>x.SchoolId==w.School),Is.EqualTo(1));
    }
    [Test]
    public async Task Outbox_lease_is_exclusive_and_stale_ack_cannot_claim_delivery()
    {
        var w=World();await w.Published();await w.Practical(true);
        await PostgresFixture.Sql(pg.Owner,$"UPDATE outbox_message SET sent_at=clock_timestamp() WHERE school_id<>'{w.School}'");
        await PostgresFixture.Sql(pg.Owner,$"UPDATE outbox_message SET next_attempt_at=clock_timestamp()-interval '1 second' WHERE school_id='{w.School}'");
        var sender=new OutboxStore(pg.Delivery);var first=await sender.ClaimAsync(default);Assert.That(first,Is.Not.Null);
        await sender.FinishAsync(first!.Id,Guid.NewGuid(),true,default);
        await using(var db=pg.OwnerDb())Assert.That((await db.Set<OutboxMessage>().SingleAsync(x=>x.Id==first.Id)).SentAt,Is.Null);
        await sender.FinishAsync(first.Id,first.LeaseId!.Value,true,default);
        await using(var db=pg.OwnerDb())Assert.That((await db.Set<OutboxMessage>().SingleAsync(x=>x.Id==first.Id)).SentAt,Is.Not.Null);
    }
}
