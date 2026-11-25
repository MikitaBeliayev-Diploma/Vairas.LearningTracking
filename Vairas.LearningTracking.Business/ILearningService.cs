using System.Security.Cryptography;
using System.Text;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

public interface ILearningService
{
    Task<PracticalRecordResult[]> PracticalRecordsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct) => throw LearningErrors.Dependency();
    Task<CommandResult<ProgramResponse>> CreateProgramAsync(CreateProgramRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<ProgramVersionResponse[]> ProgramVersionsAsync(Guid school, Guid program, LearningActor actor, CancellationToken ct);
    Task<ProgramResponse[]> ProgramsAsync(Guid school, LearningActor actor, CancellationToken ct);
    Task<CommandResult<ProgramVersionResponse>> SaveVersionAsync(Guid program, Guid? version, ProgramVersionRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<DefinitionInput[]> EnrollmentDefinitionsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct);
    Task<ProgramVersionResponse> VersionAsync(Guid school, Guid version, LearningActor actor, CancellationToken ct);
    Task<CommandResult<ProgramVersionResponse>> LifecycleAsync(Guid school, Guid version, string action, RevisionRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<CommandResult<TrainingEnrollmentResponse>> StartAsync(StartTrainingRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<LessonSourceSnapshotResult> VerifySourceAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct);
    Task<CommandResult<PracticalRecordResult>> PracticalAsync(Guid? id, PracticalFactsRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<PracticalRecordResult> PracticalReadAsync(Guid school, Guid id, LearningActor actor, CancellationToken ct);
    Task<CommandResult<PracticalRecordResult>> RespondAsync(Guid school, Guid id, long revision, ResponseKind response, string? reason, Guid operation, LearningActor actor, CancellationToken ct);
    Task<CommandResult<TheoryRecordResult>> TheoryAsync(Guid? id, TheoryFactsRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<Roster> TheoryRosterAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct);
    Task<TheoryRecordResult[]> TheoryRecordsAsync(Guid school, Guid lesson, LearningActor actor, CancellationToken ct);
    Task<TheoryRecordResult> TheoryReadAsync(Guid school, Guid id, LearningActor actor, CancellationToken ct);
    Task<CommandResult<TheoryRecordResult>> ConfirmTheoryAsync(Guid school, Guid id, long revision, Guid operation, LearningActor actor, CancellationToken ct);
    Task<ProgressResponse> ProgressAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct);
    Task<CommandResult<ReadinessResponse>> EvaluateAsync(Guid school, Guid enrollment, EvaluateReadinessRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<CommandResult<ReadinessResponse>> DecideAsync(Guid school, Guid enrollment, ReadinessDecisionRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<ReadinessResponse[]> ReadinessAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct);
    Task<CommandResult<AssessmentResponse>> AssessAsync(AssessmentResultRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<CommandResult<AssessmentResponse>> CorrectAssessmentAsync(Guid school, Guid assessment, CorrectAssessmentRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<AssessmentResponse[]> AssessmentsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct);
    Task<AssessmentVersionHistory[]> AssessmentHistoryAsync(Guid school, Guid assessment, LearningActor actor, CancellationToken ct);
    Task<CommandResult<EvidenceResponse>> PrepareEvidenceAsync(Guid school, Guid enrollment, PrepareEvidenceRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<EvidenceResponse> EvidenceAsync(Guid school, Guid snapshot, LearningActor actor, CancellationToken ct);
    Task<CommandResult<ReportingDeclarationResponse>> DeclareAsync(Guid school, Guid enrollment, ReportingDeclarationRequest request, Guid operation, LearningActor actor, CancellationToken ct);
    Task<ReportingDeclarationDetail[]> DeclarationsAsync(Guid school, Guid enrollment, LearningActor actor, CancellationToken ct);
    Task<ConfirmedPracticalRecordsResponse> ConfirmedAsync(Guid school, Guid enrollment, CancellationToken ct);
    Task<EvidenceResponse> CompletionAsync(Guid school, Guid enrollment, CancellationToken ct);
}
