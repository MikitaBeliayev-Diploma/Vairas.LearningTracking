using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

public interface ILearningIntegrationService
{
    Task<LearningPlan> PlanAsync(PlanningInputsRequest request, CancellationToken ct);
    Task<AssessmentEvidence> AssessmentEvidenceAsync(AssessmentEvidenceRequest request, CancellationToken ct);
    Task<ProgrammeApplicabilityResponse> ApplicabilityAsync(ProgrammeApplicabilityRequest request, CancellationToken ct);
    Task<TrainingRequirementEvidence> RequirementAsync(TrainingRequirementRequest request, CancellationToken ct);
    Task ConsumeAsync(IntegrationMessage message, CancellationToken ct);
    Task ReconcileEnrollmentAsync(Guid school, Guid enrollment, CancellationToken ct);
    Task ReconcileLessonAsync(Guid school, Guid lesson, CancellationToken ct);
}
