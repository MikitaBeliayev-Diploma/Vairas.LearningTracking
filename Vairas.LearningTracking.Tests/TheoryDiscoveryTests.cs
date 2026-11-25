using NUnit.Framework;
using Vairas.LearningTracking.Business;
namespace Vairas.LearningTracking.Tests;

[TestFixture, Category("Unit")]
public sealed class TheoryDiscoveryTests
{
    [Test]
    public async Task Historical_roster_and_existing_record_are_read_from_owners()
    {
        var w = new TestWorld(); await w.Published();
        var definitions = await w.Service.EnrollmentDefinitionsAsync(w.School, w.Enrollment, w.Instructor, default);
        Assert.That(definitions.Select(x => x.DefinitionVersionId), Does.Contain(w.FinalDefinition));
        var saved = await w.Theory();
        var records = await w.Service.TheoryRecordsAsync(w.School, w.TheoryLesson, w.Instructor, default);
        Assert.That(records.Select(x => x.RecordId), Does.Contain(saved.RecordId));
        var priorStudent = Guid.NewGuid(); var priorEnrollment = Guid.NewGuid();
        w.RosterOverride = new(w.Group, w.School, 1, [new(priorEnrollment, priorStudent)]);
        var roster = await w.Service.TheoryRosterAsync(w.School, w.TheoryLesson, w.Instructor, default);
        Assert.That(roster.Participants.Single().EnrollmentId, Is.EqualTo(priorEnrollment));
        Assert.That(roster.MembershipVersion, Is.EqualTo(1));
        Business.ThrowsBusinessAsync(() => w.Service.TheoryRosterAsync(w.School, w.TheoryLesson, w.Learner, default));
    }
}

