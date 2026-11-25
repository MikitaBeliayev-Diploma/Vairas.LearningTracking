using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Unit")]
public sealed class DocumentEvidenceTests
{
    [Test]
    public async Task Exact_document_payload_keeps_original_facts_and_confirmations_after_a_correction()
    {
        var w=new TestWorld();await w.Published();var record=await w.Practical(true);await w.Theory(true);
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        var original=await w.Service.DocumentEvidenceAsync(w.School,w.Enrollment,snapshot.SnapshotId,w.AdminUser,default);
        Assert.That(original.Rows.Count(x=>x.Kind=="Practical"),Is.EqualTo(1));Assert.That(original.Rows.Single(x=>x.Kind=="Practical").Confirmations,Has.Length.EqualTo(2));
        Assert.That(original.ProgramVersionId,Is.EqualTo(w.ProgramVersion));Assert.That(original.RuleSources,Is.Not.Empty);
        await w.Service.PracticalAsync(record.RecordId,record.Facts with{ExpectedRevision=record.Revision,DistanceKm=22,CorrectionReason="Correct distance."},Guid.NewGuid(),w.Instructor,default);
        var historical=await w.Service.DocumentEvidenceAsync(w.School,w.Enrollment,snapshot.SnapshotId,w.AdminUser,default);
        Assert.That(Wire.Save(historical.Rows),Is.EqualTo(Wire.Save(original.Rows)));Assert.That(historical.CurrentDependencyRevision,Is.GreaterThan(original.DependencyRevision));Assert.That(historical.IsCurrent,Is.False);
    }
    [Test]
    public async Task Owner_decision_is_stable_after_change_and_a_fresh_current_use_is_rejected()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        var request=new DocumentReleaseRequest(w.School,w.Enrollment,snapshot.SnapshotId,snapshot.DependencyRevision,Guid.NewGuid(),w.AdminUser,"PracticalJournal",new string('a',64),"Publication");
        var operation=Guid.NewGuid();var accepted=await w.Service.DecideDocumentAsync(operation,request,default);Assert.That(accepted.Status,Is.EqualTo("Accepted"));
        w.Sources[w.PracticalLesson]=w.Sources[w.PracticalLesson] with{LessonRevision=2};
        Assert.That(await w.Service.DecideDocumentAsync(operation,request,default),Is.EqualTo(accepted));
        Assert.That(await w.Service.DocumentDecisionAsync(w.School,operation,w.AdminUser,default),Is.EqualTo(accepted));
        Assert.That((await w.Service.DecideDocumentAsync(Guid.NewGuid(),request with{Purpose="CurrentUse"},default)).Status,Is.EqualTo("Rejected"));
        Business.ThrowsBusinessAsync(()=>w.Service.DecideDocumentAsync(operation,request with{ArtifactDigest=new string('b',64)},default));
    }
    [Test]
    public async Task Current_use_allows_own_student_but_does_not_grant_publication_or_foreign_snapshot_reads()
    {
        var w=new TestWorld();await w.Published();await w.Practical(true);await w.Theory(true);
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        var own=new DocumentReleaseRequest(w.School,w.Enrollment,snapshot.SnapshotId,snapshot.DependencyRevision,Guid.NewGuid(),w.StudentUser,"PracticalJournal",new string('a',64),"CurrentUse");
        Assert.That((await w.Service.DecideDocumentAsync(Guid.NewGuid(),own,default)).Status,Is.EqualTo("Accepted"));
        Business.ThrowsBusinessAsync(()=>w.Service.DecideDocumentAsync(Guid.NewGuid(),own with{Purpose="Publication"},default));
        Business.ThrowsBusinessAsync(()=>w.Service.DocumentEvidenceAsync(w.School,Guid.NewGuid(),snapshot.SnapshotId,w.AdminUser,default));
        Assert.That(await w.Service.DocumentDecisionAsync(w.School,Guid.NewGuid(),w.AdminUser,default),Is.Null);
        Assert.That(await w.Service.DocumentRevisionAsync(w.School,w.Enrollment,default),Is.EqualTo(snapshot.DependencyRevision));
    }
    [Test]
    public async Task Incomplete_or_unavailable_evidence_cannot_release_ready_documents()
    {
        var w=new TestWorld();await w.Published();await w.Practical();
        var snapshot=(await w.Service.PrepareEvidenceAsync(w.School,w.Enrollment,new(EvidenceKind.Journal),Guid.NewGuid(),w.Admin,default)).Value;
        var r=new DocumentReleaseRequest(w.School,w.Enrollment,snapshot.SnapshotId,snapshot.DependencyRevision,Guid.NewGuid(),w.AdminUser,"PracticalJournal",new string('a',64),"Publication");
        Assert.That((await w.Service.DecideDocumentAsync(Guid.NewGuid(),r,default)).Status,Is.EqualTo("Rejected"));
        Business.ThrowsBusinessAsync(()=>w.Service.DecideDocumentAsync(Guid.NewGuid(),r with{ArtifactDigest="bad"},default));
        w.AuthorityAvailable=false;Business.ThrowsBusinessAsync(()=>w.Service.DocumentRevisionAsync(w.School,w.Enrollment,default));
    }
}
