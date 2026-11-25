using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Integrations;
namespace Vairas.LearningTracking.Controllers;

[ApiController, Authorize(Policy = "LearningEntry"), Route("api/v1/student-trainings")]
public sealed class TrainingController(ILearningService service, ILearningActorFactory actors) : BaseApiController(actors)
{
    /// <summary>Read the exact assessment definitions and prerequisites pinned to an authorized enrollment.</summary>
    [HttpGet("{enrollmentId:guid}/assessment-definitions", Name="getEnrollmentAssessmentDefinitions")]
    public async Task<ActionResult<DefinitionInput[]>> Definitions(Guid enrollmentId, Guid schoolId, CancellationToken ct) =>
        Ok(await service.EnrollmentDefinitionsAsync(schoolId, enrollmentId, await Actor(ct), ct));
    /// <summary>Pin a verified published programme to one School-owned enrollment.</summary>
    [HttpPost(Name = "startStudentTraining"), ProducesResponseType<TrainingEnrollmentResponse>(201)]
    public async Task<ActionResult<TrainingEnrollmentResponse>> Start(StartTrainingRequest request, CancellationToken ct)
    { var r = await service.StartAsync(request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/student-trainings/{r.Value.EnrollmentId:D}/progress?schoolId={request.SchoolId:D}"); }
    /// <summary>Get derived current hours, deficits, pending records and verification state.</summary>
    [HttpGet("{enrollmentId:guid}/progress", Name = "getStudentProgress")]
    public async Task<ActionResult<ProgressResponse>> Progress(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.ProgressAsync(schoolId, enrollmentId, await Actor(ct), ct));
    /// <summary>Evaluate the selected final assessment's formal pre-assessment prerequisites.</summary>
    [HttpPost("{enrollmentId:guid}/readiness-evaluations", Name = "evaluateReadiness"), ProducesResponseType<ReadinessResponse>(201)]
    public async Task<ActionResult<ReadinessResponse>> Evaluate(Guid enrollmentId, Guid schoolId, EvaluateReadinessRequest request, CancellationToken ct)
    { var r = await service.EvaluateAsync(schoolId, enrollmentId, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/student-trainings/{enrollmentId:D}/readiness?schoolId={schoolId:D}"); }
    /// <summary>Qualified assessor records a separate decision after current prerequisite validation.</summary>
    [HttpPost("{enrollmentId:guid}/readiness-decisions", Name = "decideReadiness")]
    public async Task<ActionResult<ReadinessResponse>> Decide(Guid enrollmentId, Guid schoolId, ReadinessDecisionRequest request, CancellationToken ct) => Ok((await service.DecideAsync(schoolId, enrollmentId, request, Operation(), await Actor(ct), ct)).Value);
    /// <summary>Read current evaluations, decisions and invalidation state.</summary>
    [HttpGet("{enrollmentId:guid}/readiness", Name = "getReadiness")]
    public async Task<ActionResult<ReadinessResponse[]>> Readiness(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.ReadinessAsync(schoolId, enrollmentId, await Actor(ct), ct));
    /// <summary>Record a performed assessment attempt separately from scheduling and readiness.</summary>
    [HttpPost("{enrollmentId:guid}/assessments", Name = "recordAssessmentResult"), ProducesResponseType<AssessmentResponse>(201)]
    public async Task<ActionResult<AssessmentResponse>> Assess(Guid enrollmentId, AssessmentResultRequest request, CancellationToken ct)
    { if (enrollmentId != request.EnrollmentId) throw LearningErrors.Invalid(); var r = await service.AssessAsync(request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/student-trainings/{enrollmentId:D}/assessments?schoolId={request.SchoolId:D}"); }
    /// <summary>Read each attempt's latest result with current historical validity.</summary>
    [HttpGet("{enrollmentId:guid}/assessments", Name = "getAssessmentResults")]
    public async Task<ActionResult<AssessmentResponse[]>> Assessments(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.AssessmentsAsync(schoolId, enrollmentId, await Actor(ct), ct));
    /// <summary>Append an assessment correction, annulment or documented historical review.</summary>
    [HttpPost("/api/v1/assessments/{assessmentId:guid}/versions", Name = "reviseAssessmentResult")]
    public async Task<ActionResult<AssessmentResponse>> Correct(Guid assessmentId, Guid schoolId, CorrectAssessmentRequest request, CancellationToken ct) => Ok((await service.CorrectAssessmentAsync(schoolId, assessmentId, request, Operation(), await Actor(ct), ct)).Value);
    /// <summary>Read retained result versions, correction reasons and exact historical review provenance.</summary>
    [HttpGet("/api/v1/assessments/{assessmentId:guid}/versions", Name = "getAssessmentResultHistory")]
    public async Task<ActionResult<AssessmentVersionHistory[]>> History(Guid assessmentId, Guid schoolId, CancellationToken ct) => Ok(await service.AssessmentHistoryAsync(schoolId, assessmentId, await Actor(ct), ct));
    /// <summary>Prepare immutable journal or completion evidence with explicit missing checklist items.</summary>
    [HttpPost("{enrollmentId:guid}/evidence-snapshots", Name = "prepareTrainingEvidence"), ProducesResponseType<EvidenceResponse>(201)]
    public async Task<ActionResult<EvidenceResponse>> Prepare(Guid enrollmentId, Guid schoolId, PrepareEvidenceRequest request, CancellationToken ct)
    { var r = await service.PrepareEvidenceAsync(schoolId, enrollmentId, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/training-evidence/{r.Value.SnapshotId:D}?schoolId={schoolId:D}"); }
    /// <summary>Read historical evidence plus its current release eligibility.</summary>
    [HttpGet("/api/v1/training-evidence/{snapshotId:guid}", Name = "getTrainingEvidence")]
    public async Task<ActionResult<EvidenceResponse>> Evidence(Guid snapshotId, Guid schoolId, CancellationToken ct) => Ok(await service.EvidenceAsync(schoolId, snapshotId, await Actor(ct), ct));
    /// <summary>Record a declaration of manual external reporting; no submission is performed.</summary>
    [HttpPost("{enrollmentId:guid}/reporting-declarations", Name = "declareManualReporting"), ProducesResponseType<ReportingDeclarationResponse>(201)]
    public async Task<ActionResult<ReportingDeclarationResponse>> Declare(Guid enrollmentId, Guid schoolId, ReportingDeclarationRequest request, CancellationToken ct)
    { var r = await service.DeclareAsync(schoolId, enrollmentId, request, Operation(), await Actor(ct), ct); return CreatedOrReplay(r, $"/api/v1/training-evidence/{r.Value.SnapshotId:D}?schoolId={schoolId:D}"); }
    /// <summary>Read the internal manual-reporting declarations of one enrollment; they are not external acknowledgements.</summary>
    [HttpGet("{enrollmentId:guid}/reporting-declarations", Name = "getReportingDeclarations")]
    public async Task<ActionResult<ReportingDeclarationDetail[]>> Declarations(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.DeclarationsAsync(schoolId, enrollmentId, await Actor(ct), ct));
}

