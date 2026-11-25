using System.Linq.Expressions;
using System.Text.Json;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Tests;
internal sealed class TestWorld : ILearningOwners,IChecklistRegistry
{
    public readonly Guid School=Guid.NewGuid(),Enrollment=Guid.NewGuid(),Student=Guid.NewGuid(),Teacher=Guid.NewGuid(),Group=Guid.NewGuid(),Branch=Guid.NewGuid(),Vehicle=Guid.NewGuid();
    public readonly Guid AdminUser=Guid.NewGuid(),TeacherUser=Guid.NewGuid(),StudentUser=Guid.NewGuid(),ChecklistId=Guid.NewGuid(),PracticalLesson=Guid.NewGuid(),TheoryLesson=Guid.NewGuid();
    public readonly Guid Topic=Guid.NewGuid(),FinalDefinition=Guid.NewGuid(),MidtermDefinition=Guid.NewGuid();
    public Guid Program,ProgramVersion;
    public bool PolicyAvailable=true,SourceAvailable=true,AuthorityAvailable=true,GrantAssessor=true,AssociationAvailable=true;
    public long EnrollmentRevision=1,SchoolRevision=1;
    public string EnrollmentStatus="Active";
    public readonly TestClock Clock=new();
    public readonly MemoryStore Memory=new();
    public ILearningStore Store {get;set;}
    public Dictionary<Guid,TrainingSource> Sources {get;}=[];
    public Roster? RosterOverride;
    public Allocation[] Allocated=[];
    public TestWorld(Guid? student = null, Guid? studentUser = null)
    {
        if (student is Guid person) Student = person;
        if (studentUser is Guid user) StudentUser = user;
        Store=Memory;
        Sources[PracticalLesson]=new(PracticalLesson,School,1,TrainingKind.Practical,true,true,Clock.Now.AddHours(-2),Clock.Now.AddHours(-1),null,Teacher,Enrollment,Student,Vehicle,null);
        Sources[TheoryLesson]=new(TheoryLesson,School,1,TrainingKind.Theory,true,true,Clock.Now.AddHours(-3),Clock.Now.AddHours(-2),1,Teacher,null,null,null,Group);
    }
    public LearningService Service=>new(Store,this,this,Clock);
    public LearningActor Admin=>new(Guid.NewGuid(),AdminUser,null,null,Clock.Now.AddSeconds(30),Guid.NewGuid().ToString("D"));
    public LearningActor Instructor=>new(Guid.NewGuid(),TeacherUser,null,Teacher,Clock.Now.AddSeconds(30),Guid.NewGuid().ToString("D"));
    public LearningActor Learner=>new(Guid.NewGuid(),StudentUser,Student,null,Clock.Now.AddSeconds(30),Guid.NewGuid().ToString("D"));
    public ChecklistPolicy Policy=>new(ChecklistId,School,"B","synthetic","https://synthetic.invalid/checklist","test-only",AdminUser,new(2026,11,23,12,0,0,TimeSpan.Zero),new(2026,1,1),new(2027,1,1),true,
        ["Identity","ActualInterval","TeachingMinutes","Distance","Topics"],["Programme","Sources","ConfirmedPracticalRecords","ConfirmedTheoryAttendance","NoPendingReviews"]);
    public ChecklistPolicy? Find(Guid version,Guid school,string category,string pathway,DateOnly date)=>PolicyAvailable && version==ChecklistId && school==School && category=="B" && pathway=="synthetic"?Policy:null;
    public Task<LearningAuthority> AuthorityAsync(Guid school,Guid actor,Guid? enrollment,Guid? teacher,Guid? group,CancellationToken ct)
    {
        if(!AuthorityAvailable)throw LearningErrors.Dependency();
        if(school!=School || enrollment is not null && enrollment!=Enrollment)throw LearningErrors.Missing();
        var allowed=actor==Guid.Empty || actor==AdminUser || actor==TeacherUser || actor==StudentUser;
        return Task.FromResult(new LearningAuthority(school,actor,enrollment,enrollment is null?null:Student,enrollment is null && group is null?"":"B",enrollment is null?"":EnrollmentStatus,
            SchoolRevision,enrollment is null?0:EnrollmentRevision,allowed,actor==AdminUser,actor==TeacherUser,actor==TeacherUser && GrantAssessor,actor==StudentUser,actor==TeacherUser,
            group ?? (enrollment is null?null:Group),enrollment is null || !AssociationAvailable?null:ProgramVersion,group is null && enrollment is null?null:Branch,Clock.Now.AddSeconds(30),"synthetic current School authority"));
    }
    public Task<TrainingSource> SourceAsync(Guid school,Guid lesson,CancellationToken ct)=>SourceAvailable && school==School && Sources.TryGetValue(lesson,out var source)?Task.FromResult(source):throw LearningErrors.Dependency();
    public Task<Roster> RosterAsync(Guid school,Guid group,long revision,DateTimeOffset from,DateTimeOffset to,CancellationToken ct)=>Task.FromResult(RosterOverride??new Roster(Group,School,1,[new(Enrollment,Student)]));
    public Task<PlanningResources> ResourcesAsync(Guid school,Guid actor,Guid? enrollment,Guid? group,Guid teacher,CancellationToken ct)=>Task.FromResult(new PlanningResources(School,"B",Branch,[Teacher],[new(Enrollment,Student)],Clock.Now.AddSeconds(30)));
    public Task<Allocations> AllocationsAsync(Guid school,Guid[] enrollments,Guid[] groups,CancellationToken ct)=>Task.FromResult(new Allocations(School,1,Allocated));
    public Task<AssessmentAppointment> AppointmentAsync(Guid school,Guid appointment,CancellationToken ct)=>Task.FromResult(new AssessmentAppointment(School,Enrollment,FinalDefinition,Teacher,"Scheduled",Clock.Now.AddMinutes(-5),Clock.Now.AddMinutes(5)));
    public ProgramVersionRequest Curriculum(long revision=1,int theory=60,int practical=60)=>new(School,revision,"synthetic",theory,practical,60,60,new(2026,1,1),new(2027,1,1),ChecklistId,
        [new(Topic,"topic-1","Synthetic topic")],
        [new(Guid.NewGuid(),MidtermDefinition,"midterm","Synthetic midterm",TrainingKind.Theory,AssessmentStage.Midterm,0,10,7,false,[Topic],[]),
         new(Guid.NewGuid(),FinalDefinition,"final","Synthetic final",TrainingKind.Practical,AssessmentStage.Final,0,10,7,true,[Topic],
            [new(Guid.NewGuid(),"practical",RequirementKind.MinimumPracticalMinutes,practical),new(Guid.NewGuid(),"theory",RequirementKind.MinimumTheoryMinutes,theory)])],
        [new(Guid.NewGuid(),"synthetic-rule","https://synthetic.invalid/rule","test-only","synthetic tests only",new(2026,1,1),new(2027,1,1),"minutes",true)],
        [new(TrainingKind.Theory,60,true,15),new(TrainingKind.Practical,60,true,15)]);
    public async Task Published(int theory=60,int practical=60)
    {
        var program=await Service.CreateProgramAsync(new(School,"B","Synthetic programme"),Guid.NewGuid(),Admin,default);Program=program.Value.ProgramId;
        var version=await Service.SaveVersionAsync(Program,null,Curriculum(program.Value.Revision,theory,practical),Guid.NewGuid(),Admin,default);ProgramVersion=version.Value.ProgramVersionId;
        await Service.LifecycleAsync(School,ProgramVersion,"publish",new(version.Value.Revision),Guid.NewGuid(),Admin,default);
        await Service.StartAsync(new(School,Enrollment,ProgramVersion),Guid.NewGuid(),Admin,default);
    }
    public async Task<PracticalRecordResult> Practical(bool confirmed=false)
    {
        var source=await Service.VerifySourceAsync(School,PracticalLesson,Instructor,default);
        var record=(await Service.PracticalAsync(null,new(School,Enrollment,PracticalLesson,ProgramVersion,source.SourceSnapshotId,60,12,[Topic]),Guid.NewGuid(),Instructor,default)).Value;
        if(!confirmed)return record;
        record=(await Service.RespondAsync(School,record.RecordId,record.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),Instructor,default)).Value;
        return (await Service.RespondAsync(School,record.RecordId,record.Revision,ResponseKind.Confirm,null,Guid.NewGuid(),Learner,default)).Value;
    }
    public async Task<TheoryRecordResult> Theory(bool confirmed=false,Attendance attendance=Attendance.Present)
    {
        var source=await Service.VerifySourceAsync(School,TheoryLesson,Instructor,default);
        var record=(await Service.TheoryAsync(null,new(School,TheoryLesson,source.SourceSnapshotId,60,[new(Enrollment,Student,attendance)]),Guid.NewGuid(),Instructor,default)).Value;
        return confirmed?(await Service.ConfirmTheoryAsync(School,record.RecordId,record.Revision,Guid.NewGuid(),Instructor,default)).Value:record;
    }
    public async Task<ReadinessResponse> Approved()
    {
        var evaluation=(await Service.EvaluateAsync(School,Enrollment,new(FinalDefinition),Guid.NewGuid(),Learner,default)).Value;
        return (await Service.DecideAsync(School,Enrollment,new(evaluation.EvaluationId,Decision.Approved,"Observed synthetic competence",evaluation.Revision),Guid.NewGuid(),Instructor,default)).Value;
    }
}
