extern alias SchoolTests;
extern alias SchedulingApi;
extern alias AuthApi;
extern alias UserApi;
extern alias LearningApi;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.OpenApi;
using Npgsql;
using NUnit.Framework;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration;
using Vairas.LearningTracking.Domain;
using Vairas.Scheduling.Persistance;
using SchoolEnvironment=SchoolTests::Vairas.SchoolManagement.Tests.IntegrationEnvironment;
using TestUser=SchoolTests::Vairas.SchoolManagement.Tests.TestUser;
using UmJwt=UserApi::Vairas.UserManagement.Configuration.Options.JwtOptions;
using Vairas.LearningTracking.Configuration.Options;
namespace Vairas.LearningTracking.Tests;

[TestFixture,Category("Integration"),NonParallelizable]
public sealed partial class FiveServiceTests
{
    static partial void ConfigureDocumentAuth(Dictionary<string,string?> configuration);
    private PostgresFixture pg=null!;
    private SchoolEnvironment env=null!;
    private ServiceFactory<LearningApi::Program>? learningFactory;
    private ServiceFactory<SchedulingApi::Program>? schedulingFactory;
    private HttpClient learning=null!,scheduling=null!;
    private string schedulingOwner="",schedulingRuntime="",schedulingAuthority="",schedulingDelivery="",schedulingDatabase="";
    private Dictionary<string,string> schedulingRoles=[];
    private Guid school,enrollment,student,teacher,group,branch,program,version,topic,final,practicalLesson,theoryLesson;
    private ChecklistPolicy policy=null!;
    [OneTimeSetUp]
    public async Task Start()
    {
        pg=new();await pg.Start();env=new();
        env.ConfigureAuth=c=>
        {
            c["Authentication:UserAudiences:3"]="vairas-scheduling";c["Authentication:UserAudiences:4"]="vairas-learning-tracking";
            c["Authentication:WorkloadClients:learning-tracking:SecretHash"]=c["Authentication:WorkloadClients:school-management:SecretHash"];
            SetScopes(c,"learning-tracking",["identity.validate","profiles.status.read","profiles.binding.read","school.learning.read","school.participants.read","scheduling.source.read"]);
            SetScopes(c,"scheduling",["identity.validate","profiles.status.read","profiles.binding.read","school.scheduling.read","school.coordination.read","school.eligibility.read","school.participants.read","learning.planning.read","learning.readiness.read"]);
            SetScopes(c,"school-management",["identity.validate","profiles.status.read","profiles.binding.read","scheduling.school-gate","learning.programmes.read","learning.evidence.read"]);
            ConfigureDocumentAuth(c);
        };
        env.ConfigureUsers=services=>services.Configure<UmJwt>(o=>
        {o.WorkloadClients["learning-tracking"]=["profiles.status.read","profiles.binding.read"];o.WorkloadClients["scheduling"]=["profiles.status.read","profiles.binding.read"];o.WorkloadClients["document-management"]=["profiles.status.read","profiles.binding.read","profiles.documents.read"];});
        env.ConfigureSchool=c=>
        {
            c["SchedulingDependency:Enabled"]="true";c["SchedulingDependency:LearningBaseUrl"]="https://localhost:7500/";
            c["Dependencies:Scope"]="identity.validate profiles.status.read profiles.binding.read scheduling.school-gate learning.programmes.read learning.evidence.read";
            var incoming=new[]{"school.learning.read","school.participants.read"};for(var i=0;i<incoming.Length;i++)c[$"Authentication:WorkloadClients:learning-tracking:{i}"]=incoming[i];
            c["Authentication:WorkloadClients:scheduling:2"]="school.scheduling.read";c["Authentication:WorkloadClients:scheduling:3"]="school.coordination.read";
            c["Authentication:WorkloadClients:document-management:0"]="school.documents.read";
        };
        await env.StartAsync();await StartSchedulingDatabase();
        var schedulingConfig=new Dictionary<string,string?>
        {
            ["ConnectionStrings:Scheduling"]=schedulingRuntime,["ConnectionStrings:ReservationAuthority"]=schedulingAuthority,
            ["Dependencies:ClientSecret"]=env.Secret,["Dependencies:LearningBaseUrl"]="https://localhost:7500/",
            ["Dependencies:Scope"]="identity.validate profiles.status.read profiles.binding.read school.scheduling.read school.coordination.read school.eligibility.read school.participants.read learning.planning.read learning.readiness.read",
            ["Authentication:WorkloadClients:school-management:0"]="scheduling.school-gate",["Authentication:WorkloadClients:learning-tracking:0"]="scheduling.source.read",["Scheduling:SyntheticMode"]="true"
        };
        schedulingFactory=new(Path.Combine(PostgresFixture.Root,"../Vairas.Scheduling/Vairas.Scheduling"),schedulingConfig,env);
        scheduling=schedulingFactory.CreateClient(new(){BaseAddress=new("https://localhost:7300")});env.Scheduling=scheduling;
        school=await env.SeedSchool();
        var now=DateTimeOffset.UtcNow;
        policy=new(Guid.NewGuid(),school,"B","synthetic","https://synthetic.invalid/checklist","fixture-only",env.Administrator.UserId,now.AddDays(-1),new(2026,1,1),new(2027,1,1),true,
            ["Identity","ActualInterval","TeachingMinutes","Distance","Topics"],["Programme","Sources","ConfirmedPracticalRecords","ConfirmedTheoryAttendance","NoPendingReviews"]);
        var learningConfig=new Dictionary<string,string?>
        {
            ["ConnectionStrings:LearningTracking"]=pg.Runtime,["Dependencies:ClientSecret"]=env.Secret,
            ["Authentication:WorkloadClients:scheduling:0"]="learning.planning.read",["Authentication:WorkloadClients:scheduling:1"]="learning.readiness.read",
            ["Authentication:WorkloadClients:school-management:0"]="learning.programmes.read",["Authentication:WorkloadClients:school-management:1"]="learning.evidence.read"
            ,["Authentication:WorkloadClients:document-management:0"]="learning.documents.read",["Authentication:WorkloadClients:document-management:1"]="learning.documents.decide"
        };
        learningFactory=new(Path.Combine(PostgresFixture.Root,"Vairas.LearningTracking"),learningConfig,env,services=>services.AddSingleton(new ChecklistOptions{SyntheticMode=true,Policies=[policy]}));
        learning=learningFactory.CreateClient(new(){BaseAddress=new("https://localhost:7500")});env.Learning=learning;
        await SetupActorsAndSchool();await SetupProgrammeAndAssociation();await SeedCompletedSchedulingSources();
    }
    private static void SetScopes(Dictionary<string,string?> c,string client,string[] scopes)
    {for(var i=0;i<scopes.Length;i++)c[$"Authentication:WorkloadClients:{client}:Scopes:{i}"]=scopes[i];}
    private async Task StartSchedulingDatabase()
    {
        var suffix=Guid.NewGuid().ToString("N");var secret=env.Secret;
        schedulingRoles=new(){["v_scheduling_migration"]="lsc_owner_"+suffix,["v_scheduling_runtime"]="lsc_runtime_"+suffix,["v_scheduling_reservation_authority"]="lsc_authority_"+suffix,["v_scheduling_outbox"]="lsc_delivery_"+suffix};
        foreach(var role in schedulingRoles.Values)await PostgresFixture.Sql(pg.AdminConnection,$"CREATE ROLE {role} LOGIN PASSWORD '{secret}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS");
        schedulingDatabase="learning_scheduling_"+suffix;await PostgresFixture.Sql(pg.AdminConnection,$"CREATE DATABASE {schedulingDatabase} OWNER {schedulingRoles["v_scheduling_migration"]}");
        var baseConnection=new NpgsqlConnectionStringBuilder(pg.AdminConnection){Database=schedulingDatabase};
        string Connection(string role)=>new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString){Username=schedulingRoles[role],Password=secret}.ConnectionString;
        schedulingOwner=Connection("v_scheduling_migration");schedulingRuntime=Connection("v_scheduling_runtime");schedulingAuthority=Connection("v_scheduling_reservation_authority");schedulingDelivery=Connection("v_scheduling_outbox");
        await PostgresFixture.Sql(baseConnection.ConnectionString,$"ALTER SCHEMA public OWNER TO {schedulingRoles["v_scheduling_migration"]}");
        await using(var db=new SchedulingDbContext(new DbContextOptionsBuilder<SchedulingDbContext>().UseScheduling(schedulingOwner).Options))await db.Database.MigrateAsync();
        foreach(var file in new[]{"database-roles.sql","reliability-grants.sql","review-grants.sql"})
        {var sql=await File.ReadAllTextAsync(Path.Combine(PostgresFixture.Root,"../Vairas.Scheduling/database",file));foreach(var pair in schedulingRoles)sql=sql.Replace(pair.Key,pair.Value);await PostgresFixture.Sql(schedulingOwner,sql);}
    }
    private async Task<JsonElement> Send(HttpClient client,HttpMethod method,string route,TestUser user,object? body=null,bool command=false)
    {
        using var request=new HttpRequestMessage(method,route);
        request.Headers.Authorization=new("Bearer",user.Tokens.AccessToken);
        if(command)request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString("D"));
        if(body is not null)request.Content=JsonContent.Create(body,body.GetType(),options:Wire.Json);
        using var response=await client.SendAsync(request);
        var text=await response.Content.ReadAsStringAsync();Assert.That(response.IsSuccessStatusCode,Is.True,$"{method} {route}: {(int)response.StatusCode} {text}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }
    [Test]
    public async Task Programme_versions_are_discoverable_and_school_scoped()
    {
        var rows = await Send(learning, HttpMethod.Get, $"/api/v1/training-programs/{program}/versions?schoolId={school}", env.Administrator);
        Assert.That(rows.EnumerateArray().Select(x => x.GetProperty("programVersionId").GetGuid()), Does.Contain(version));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/training-programs/{program}/versions?schoolId={Guid.NewGuid()}");
        request.Headers.Authorization = new("Bearer", env.Administrator.Tokens.AccessToken);
        using var response = await learning.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden).Or.EqualTo(HttpStatusCode.NotFound));
    }
    [Test]
    public async Task Theory_discovery_uses_historical_roster_and_enrollment_definitions()
    {
        var roster = await Send(learning, HttpMethod.Get, $"/api/v1/lessons/{theoryLesson}/theory-roster?schoolId={school}", env.Target);
        Assert.That(roster.GetProperty("participants").EnumerateArray().Select(x => x.GetProperty("enrollmentId").GetGuid()), Does.Contain(enrollment));
        var records = await Send(learning, HttpMethod.Get, $"/api/v1/lessons/{theoryLesson}/theory-records?schoolId={school}", env.Target);
        Assert.That(records.ValueKind, Is.EqualTo(JsonValueKind.Array));
        var definitions = await Send(learning, HttpMethod.Get, $"/api/v1/student-trainings/{enrollment}/assessment-definitions?schoolId={school}", env.Target);
        Assert.That(definitions.EnumerateArray().Select(x => x.GetProperty("definitionVersionId").GetGuid()), Does.Contain(final));
        var appointments = await Send(scheduling, HttpMethod.Get, $"/api/v1/assessment-appointments?schoolId={school}&enrollmentId={enrollment}", env.Administrator);
        Assert.That(appointments.ValueKind, Is.EqualTo(JsonValueKind.Array));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/lessons/{theoryLesson}/theory-roster?schoolId={Guid.NewGuid()}");
        request.Headers.Authorization = new("Bearer", env.Target.Tokens.AccessToken);
        using var response = await learning.SendAsync(request);
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.NotFound, HttpStatusCode.Forbidden));
    }
    private async Task SetupActorsAndSchool()
    {
        var invitation=await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/invitations",env.Administrator,new{targetUserId=env.Target.UserId,role="Teacher"},true);
        var invitationId=invitation.GetProperty("resource").GetProperty("invitationId").GetGuid();
        await Send(env.Users,HttpMethod.Post,"/api/v1/users/me/teacher-profile",env.Target,new{schoolId=school,invitationId},true);
        await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/invitations/{invitationId}/accept",env.Target,new{expectedRevision=1},true);
        teacher=(await Send(env.Users,HttpMethod.Get,"/api/v1/users/me",env.Target)).GetProperty("teacherId").GetGuid();
        await Send(env.Users,HttpMethod.Post,"/api/v1/users/me/student-profile",env.Outsider,new{},true);
        student=(await Send(env.Users,HttpMethod.Get,"/api/v1/users/me",env.Outsider)).GetProperty("studentId").GetGuid();
        enrollment=(await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/enrollments",env.Administrator,new{studentId=student,licenseCategory="B"},true)).GetProperty("resultId").GetGuid();
        branch=(await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/branches",env.Administrator,new{name="Synthetic branch"},true)).GetProperty("resource").GetProperty("branchId").GetGuid();
        group=(await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/groups",env.Administrator,new{name="Synthetic group",branchId=branch,categoryCode="B"},true)).GetProperty("resource").GetProperty("groupId").GetGuid();
        // Assessor grant is genuine School authority under the configured synthetic platform operator.
        using(var db=env.OwnerDb())
        {
            var membership=await db.Set<global::Vairas.SchoolManagement.Domain.SchoolMembership>().SingleAsync(x=>x.SchoolId==school && x.UserId==env.Target.UserId);
            await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/memberships/{membership.MembershipId}/permissions",env.Administrator,new{permission="Assessor"},true);
        }
        await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/groups/{group}/teacher-assignments",env.Administrator,new{teacherId=teacher,expectedRevision=1},true);
    }
    private async Task SetupProgrammeAndAssociation()
    {
        program=(await Send(learning,HttpMethod.Post,"/api/v1/training-programs",env.Administrator,new CreateProgramRequest(school,"B","Synthetic interop programme"),true)).GetProperty("programId").GetGuid();
        topic=Guid.NewGuid();final=Guid.NewGuid();
        var request=new ProgramVersionRequest(school,1,"synthetic",60,60,60,60,new(2026,1,1),new(2027,1,1),policy.VersionId,
            [new(topic,"synthetic-topic","Synthetic topic")],[new(Guid.NewGuid(),final,"final","Synthetic final",TrainingKind.Theory,AssessmentStage.Final,0,10,7,true,[topic],
                [new(Guid.NewGuid(),"theory",RequirementKind.MinimumTheoryMinutes,60),new(Guid.NewGuid(),"practical",RequirementKind.MinimumPracticalMinutes,60)])],
            [new(Guid.NewGuid(),"synthetic","https://synthetic.invalid/rule","fixture-only","synthetic tests only",new(2026,1,1),new(2027,1,1),"minutes",true)],
            [new(TrainingKind.Theory,60,true,15),new(TrainingKind.Practical,60,true,15)]);
        var draft=await Send(learning,HttpMethod.Post,$"/api/v1/training-programs/{program}/versions",env.Administrator,request,true);version=draft.GetProperty("programVersionId").GetGuid();
        await Send(learning,HttpMethod.Post,$"/api/v1/training-program-versions/{version}/publish?schoolId={school}",env.Administrator,new RevisionRequest(draft.GetProperty("revision").GetInt64()),true);
        var e=await Send(env.Client,HttpMethod.Get,$"/api/v1/schools/{school}/enrollments/{enrollment}",env.Administrator);
        await Send(env.Client,HttpMethod.Put,$"/api/v1/schools/{school}/enrollments/{enrollment}/group-assignment",env.Administrator,new{groupId=group,programVersionId=version,expectedRevision=e.GetProperty("version").GetInt64()},true);
        await Send(learning,HttpMethod.Post,"/api/v1/student-trainings",env.Administrator,new StartTrainingRequest(school,enrollment,version),true);
    }
    private async Task SeedCompletedSchedulingSources()
    {
        practicalLesson=Guid.NewGuid();theoryLesson=Guid.NewGuid();
        long roster;
        using(var db=env.OwnerDb())roster=(await db.Set<global::Vairas.SchoolManagement.Domain.TrainingGroup>().SingleAsync(x=>x.SchoolId==school && x.GroupId==group)).MembershipRevision+1;
        var historicalOperation=Guid.NewGuid();
        // Add a separate retained historical source fixture; never rewrite an existing immutable roster revision.
        await SchoolEnvironment.Sql(env.SchoolOwner,$"""
            INSERT INTO school_operation(operation_id,school_id,command_name,target_kind,target_id,actor_user_id,channel,status,request_fingerprint,requested_at,resolved_at,authority_basis)
            VALUES('{historicalOperation}','{school}','IsolatedHistoricalRosterFixture','Group','{group}','{env.Administrator.UserId}','Internal','Committed','{new string('a',64)}',clock_timestamp(),clock_timestamp(),'synthetic retained-source fixture');
            UPDATE training_group SET started_at=clock_timestamp()-interval '2 days',membership_revision={roster} WHERE school_id='{school}' AND group_id='{group}';
            INSERT INTO group_roster_revision(group_id,school_id,membership_revision,recorded_at,operation_id)
            VALUES('{group}','{school}',{roster},clock_timestamp(),'{historicalOperation}');
            INSERT INTO group_roster_member(group_id,school_id,membership_revision,membership_id,enrollment_id,student_id,enrollment_revision,enrollment_status,category_code,enrollment_started_at,enrollment_ended_at,joined_at,left_at)
            SELECT '{group}','{school}',{roster},m.membership_id,e.enrollment_id,e.student_id,e.revision,e.status,e.category_code,clock_timestamp()-interval '2 days',NULL,clock_timestamp()-interval '2 days',NULL
            FROM student_enrollment e JOIN student_group_membership m ON m.school_id=e.school_id AND m.enrollment_id=e.enrollment_id
            WHERE e.school_id='{school}' AND e.enrollment_id='{enrollment}' AND m.group_id='{group}' AND m.left_at IS NULL;
            """);
        // These two pre-existing completed Scheduling lessons are synthetic source fixtures, not a claim of Vehicle booking validation.
        var now=DateTimeOffset.UtcNow;var from=now.AddHours(-3);var to=from.AddHours(1);var vehicle=Guid.NewGuid();
        await PostgresFixture.Sql(schedulingOwner,$"""
            INSERT INTO lesson(id,school_id,kind,teacher_id,student_id,enrollment_id,vehicle_id,group_id,branch_id,category_code,starts_at,ends_at,status,revision,created_at,actual_start,actual_end)
            VALUES('{practicalLesson}','{school}','Practical','{teacher}','{student}','{enrollment}','{vehicle}',NULL,'{branch}','B','{from:O}','{to:O}','Completed',1,clock_timestamp(),'{from:O}','{to:O}');
            INSERT INTO lesson(id,school_id,kind,teacher_id,group_id,branch_id,category_code,starts_at,ends_at,status,revision,created_at,actual_start,actual_end,group_membership_revision)
            VALUES('{theoryLesson}','{school}','Theory','{teacher}','{group}','{branch}','B','{to:O}','{to.AddHours(1):O}','Completed',1,clock_timestamp(),'{to:O}','{to.AddHours(1):O}',{roster});
            """);
    }
    [OneTimeTearDown]
    public async Task Stop()
    {
        learning?.Dispose();scheduling?.Dispose();if(learningFactory is not null)await learningFactory.DisposeAsync();if(schedulingFactory is not null)await schedulingFactory.DisposeAsync();
        if(env is not null)await env.DisposeAsync();
        if(pg is not null)
        {
            NpgsqlConnection.ClearAllPools();
            if(schedulingDatabase.Length>0){await PostgresFixture.Sql(pg.AdminConnection,$"DROP DATABASE IF EXISTS {schedulingDatabase} WITH(FORCE)");foreach(var role in schedulingRoles.Values)await PostgresFixture.Sql(pg.AdminConnection,$"DROP ROLE IF EXISTS {role}");}
            await pg.DisposeAsync();
        }
    }
    [Test,Order(1)]
    public async Task Actual_JWT_pipeline_enforces_audience_scope_school_and_current_session()
    {
        using var anonymous=await learning.GetAsync($"/api/v1/student-trainings/{enrollment}/progress?schoolId={school}");Assert.That(anonymous.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
        using var wrongKind=await SchoolEnvironment.Send(learning,HttpMethod.Get,$"/internal/v1/student-trainings/{enrollment}/completion-evidence?schoolId={school}",env.Administrator.Tokens.AccessToken);Assert.That(wrongKind.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
        var stranger=await env.NewUser();using var hidden=await SchoolEnvironment.Send(learning,HttpMethod.Get,$"/api/v1/student-trainings/{enrollment}/progress?schoolId={school}",stranger.Tokens.AccessToken);Assert.That(hidden.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
        env.FailAuth=true;
        try{using var denied=await SchoolEnvironment.Send(learning,HttpMethod.Get,$"/api/v1/student-trainings/{enrollment}/progress?schoolId={school}",env.Outsider.Tokens.AccessToken);Assert.That(denied.StatusCode,Is.EqualTo(HttpStatusCode.ServiceUnavailable));}
        finally{env.FailAuth=false;}
    }
    [Test,Order(2)]
    public async Task Real_Scheduling_sources_and_School_roster_drive_confirmed_progress_readiness_and_completion()
    {
        var ps=await Send(learning,HttpMethod.Post,$"/api/v1/lessons/{practicalLesson}/source-verifications?schoolId={school}",env.Target);
        var p=await Send(learning,HttpMethod.Post,"/api/v1/practical-records",env.Target,new PracticalFactsRequest(school,enrollment,practicalLesson,version,ps.GetProperty("sourceSnapshotId").GetGuid(),60,15,[topic]),true);
        var record=p.GetProperty("recordId").GetGuid();
        p=await Send(learning,HttpMethod.Post,$"/api/v1/practical-records/{record}/confirmations?schoolId={school}",env.Target,new ConfirmationRequest(p.GetProperty("revision").GetInt64()),true);
        var pending=await Send(learning,HttpMethod.Get,$"/api/v1/student-trainings/{enrollment}/progress?schoolId={school}",env.Outsider);Assert.That(pending.GetProperty("practicalMinutes").GetInt32(),Is.Zero);
        await Send(learning,HttpMethod.Post,$"/api/v1/practical-records/{record}/confirmations?schoolId={school}",env.Outsider,new ConfirmationRequest(p.GetProperty("revision").GetInt64()),true);
        var ts=await Send(learning,HttpMethod.Post,$"/api/v1/lessons/{theoryLesson}/source-verifications?schoolId={school}",env.Target);
        var t=await Send(learning,HttpMethod.Post,"/api/v1/theory-records",env.Target,new TheoryFactsRequest(school,theoryLesson,ts.GetProperty("sourceSnapshotId").GetGuid(),60,[new(enrollment,student,Attendance.Present)]),true);
        await Send(learning,HttpMethod.Post,$"/api/v1/theory-records/{t.GetProperty("recordId").GetGuid()}/confirmations?schoolId={school}",env.Target,new ConfirmationRequest(t.GetProperty("revision").GetInt64()),true);
        var progress=await Send(learning,HttpMethod.Get,$"/api/v1/student-trainings/{enrollment}/progress?schoolId={school}",env.Outsider);Assert.That(progress.GetProperty("percent").GetDecimal(),Is.EqualTo(100));
        var e=await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/readiness-evaluations?schoolId={school}",env.Outsider,new EvaluateReadinessRequest(final),true);
        var d=await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/readiness-decisions?schoolId={school}",env.Target,new ReadinessDecisionRequest(e.GetProperty("evaluationId").GetGuid(),Decision.Approved,"Synthetic assessment readiness",e.GetProperty("revision").GetInt64()),true);
        await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/assessments",env.Target,new AssessmentResultRequest(school,enrollment,final,8,DateTimeOffset.UtcNow,d.GetProperty("decisionId").GetGuid()),true);
        var evidence=await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/evidence-snapshots?schoolId={school}",env.Administrator,new PrepareEvidenceRequest(EvidenceKind.CompletionReport),true);
        Assert.That(evidence.GetProperty("status").GetString(),Is.EqualTo("Ready"));
        var status=await Send(env.Client,HttpMethod.Get,$"/api/v1/schools/{school}/enrollments/{enrollment}",env.Administrator);
        await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/enrollments/{enrollment}/group-removal",env.Administrator,new{groupId=group,programVersionId=version,expectedRevision=status.GetProperty("version").GetInt64()},true);
        var afterRemoval=await Send(learning,HttpMethod.Post,$"/api/v1/student-trainings/{enrollment}/evidence-snapshots?schoolId={school}",env.Administrator,new PrepareEvidenceRequest(EvidenceKind.CompletionReport),true);
        Assert.That(afterRemoval.GetProperty("status").GetString(),Is.EqualTo("Ready"),afterRemoval.ToString());
        status=await Send(env.Client,HttpMethod.Get,$"/api/v1/schools/{school}/enrollments/{enrollment}",env.Administrator);
        await Send(env.Client,HttpMethod.Post,$"/api/v1/schools/{school}/enrollments/{enrollment}/termination",env.Administrator,new{expectedRevision=status.GetProperty("version").GetInt64(),status="Completed"},true);
        Assert.That((await Send(env.Client,HttpMethod.Get,$"/api/v1/schools/{school}/enrollments/{enrollment}",env.Administrator)).GetProperty("status").GetString(),Is.EqualTo("Completed"));
        using(var db=env.OwnerDb())
        {
            var events=await db.Set<global::Vairas.SchoolManagement.Domain.SchoolLearningOutbox>().Where(x=>x.SchoolId==school).ToArrayAsync();
            var samples=events.Select(e=>new{eventType=e.EventType,headers=new{eventId=e.Id,eventType=e.EventType,eventVersion=1,occurredAt=e.OccurredAt,producer="school",correlationId=e.CorrelationId,aggregateId=e.AggregateId,aggregateVersion=e.AggregateVersion},payload=JsonDocument.Parse(e.Payload).RootElement.Clone()});
            Directory.CreateDirectory(Path.Combine(PostgresFixture.Root,"evidence"));
            await File.WriteAllTextAsync(Path.Combine(PostgresFixture.Root,"evidence/school-event-samples.json"),JsonSerializer.Serialize(new{origin="real five-service HTTP pipelines and PostgreSQL with synthetic accounts",events=samples},Wire.Json));
            var history=await db.Set<global::Vairas.SchoolManagement.Domain.EnrollmentStatusHistory>().SingleAsync(x=>x.SchoolId==school && x.EnrollmentId==enrollment && x.ToStatus==global::Vairas.SchoolManagement.Domain.EnrollmentStatus.Completed);
            Assert.That(history.CompletionEvidenceId,Is.Not.Null);Assert.That(history.CompletionDependencyRevision,Is.GreaterThan(0));
        }
    }
    [Test,Order(3)]
    public async Task Generated_OpenAPI_is_parseable_and_matches_the_real_controller_pipeline()
    {
        using var response=await learning.GetAsync("/openapi/v1.json");var json=await response.Content.ReadAsStringAsync();Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK),json);
        var parsed=OpenApiDocument.Parse(json,"json");Assert.That(parsed.Diagnostic!.Errors,Is.Empty);Assert.That(parsed.Document!.Validate(ValidationRuleSet.GetDefaultRuleSet()).ToArray(),Is.Empty);
        await File.WriteAllTextAsync(Path.Combine(PostgresFixture.Root,"api/learning-tracking.openapi.json"),json);
        using(var document=JsonDocument.Parse(json))
        {
            var values=document.RootElement.GetProperty("components").GetProperty("schemas").GetProperty("TrainingKind").GetProperty("enum").EnumerateArray().ToArray();
            Assert.That(values.All(x=>x.ValueKind==JsonValueKind.String),Is.True);
            Assert.That(values.Select(x=>x.GetString()),Is.EquivalentTo(new[]{"Theory","Practical"}));
        }
        await File.WriteAllTextAsync(Path.Combine(PostgresFixture.Root,"api/learning-tracking.openapi.yaml"),await parsed.Document!.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_1));
        Assert.That(parsed.Document!.Info!.Title,Does.Contain("Learning Tracking"));await File.WriteAllTextAsync(Path.Combine(PostgresFixture.Root,"api/learning-tracking.openapi.json"),json);
        using var ready=await learning.GetAsync("/health/ready");Assert.That(ready.StatusCode,Is.EqualTo(HttpStatusCode.OK),await ready.Content.ReadAsStringAsync());
    }
    private sealed class ServiceFactory<T>(string root,Dictionary<string,string?> config,SchoolEnvironment env,Action<IServiceCollection>? extra=null):WebApplicationFactory<T> where T:class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");builder.UseContentRoot(root);builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(config));builder.ConfigureLogging(l=>l.ClearProviders());
            builder.ConfigureTestServices(services=>
            {
                services.AddHttpClient("dependencies").ConfigurePrimaryHttpMessageHandler(()=>new Bridge(env));
                foreach(var name in new[]{"User","Workload"})services.PostConfigure<JwtBearerOptions>(name,o=>{o.Authority="https://localhost:7100";o.MetadataAddress="https://localhost:7100/.well-known/openid-configuration";o.ConfigurationManager=new ConfigurationManager<OpenIdConnectConfiguration>(o.MetadataAddress,new OpenIdConnectConfigurationRetriever(),new HttpDocumentRetriever(env.Auth){RequireHttps=true});});
                extra?.Invoke(services);
            });
        }
    }
    private sealed class Bridge(SchoolEnvironment env):HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken ct)
        {
            var client=message.RequestUri!.Port switch{7100=>env.FailAuth?null:env.Auth,7001=>env.FailUsers?null:env.Users,7200=>env.Client,7300=>env.Scheduling,7500=>env.Learning,_=>throw new HttpRequestException("Unexpected test origin.")};
            if(client is null)return new(HttpStatusCode.ServiceUnavailable);
            using var forward=new HttpRequestMessage(message.Method,message.RequestUri);foreach(var h in message.Headers)forward.Headers.TryAddWithoutValidation(h.Key,h.Value);
            if(message.Content is not null){forward.Content=new ByteArrayContent(await message.Content.ReadAsByteArrayAsync(ct));foreach(var h in message.Content.Headers)forward.Content.Headers.TryAddWithoutValidation(h.Key,h.Value);}
            var response = await client.SendAsync(forward,ct);
            if (!response.IsSuccessStatusCode) TestContext.Out.WriteLine($"Dependency {message.Method} {message.RequestUri.AbsolutePath}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
            return response;
        }
    }
}


