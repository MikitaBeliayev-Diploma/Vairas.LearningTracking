using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.LearningTracking.Business;
namespace Vairas.LearningTracking.Controllers;

[ApiController, Route("internal/v1")]
public sealed class InternalController(ILearningService service, ILearningIntegrationService integration) : ControllerBase
{
    /// <summary>Return exact current confirmed practical versions for an authorized document workload.</summary>
    [HttpGet("student-trainings/{enrollmentId:guid}/confirmed-practical-records", Name = "getConfirmedPracticalRecords"), Authorize(Policy = "learning.evidence.read")]
    public async Task<ActionResult<ConfirmedPracticalRecordsResponse>> Confirmed(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.ConfirmedAsync(schoolId, enrollmentId, ct));
    /// <summary>Return prepared completion evidence and its current release eligibility.</summary>
    [HttpGet("student-trainings/{enrollmentId:guid}/completion-evidence", Name = "getCompletionEvidence"), Authorize(Policy = "learning.evidence.read")]
    public async Task<ActionResult<EvidenceResponse>> Completion(Guid enrollmentId, Guid schoolId, CancellationToken ct) => Ok(await service.CompletionAsync(schoolId, enrollmentId, ct));
    /// <summary>Resolve finite versioned curriculum demands for the exact Scheduling input basis.</summary>
    [HttpPost("scheduling/planning-inputs", Name = "getLearningPlanningInputs"), Authorize(Policy = "learning.planning.read")]
    public async Task<ActionResult<LearningPlan>> Plan(PlanningInputsRequest request, CancellationToken ct) => Ok(await integration.PlanAsync(request, ct));
    /// <summary>Verify the exact current assessment applicability and readiness basis.</summary>
    [HttpPost("scheduling/assessment-evidence", Name = "getLearningAssessmentEvidence"), Authorize(Policy = "learning.readiness.read")]
    public async Task<ActionResult<AssessmentEvidence>> Assessment(AssessmentEvidenceRequest request, CancellationToken ct) => Ok(await integration.AssessmentEvidenceAsync(request, ct));
    /// <summary>Report whether an additional lesson of the given kind addresses an outstanding requirement of the enrollment (US-010).</summary>
    [HttpPost("scheduling/training-requirement", Name = "getLearningTrainingRequirement"), Authorize(Policy = "learning.readiness.read")]
    public async Task<ActionResult<TrainingRequirementEvidence>> Requirement(TrainingRequirementRequest request, CancellationToken ct) => Ok(await integration.RequirementAsync(request, ct));
    /// <summary>Verify a prospective School association without circularly requiring the association itself.</summary>
    [HttpPost("scheduling/programme-applicability", Name = "checkProgrammeApplicability"), Authorize(Policy = "learning.programmes.read")]
    public async Task<ActionResult<ProgrammeApplicabilityResponse>> Applicability(ProgrammeApplicabilityRequest request, CancellationToken ct) => Ok(await integration.ApplicabilityAsync(request, ct));
}
