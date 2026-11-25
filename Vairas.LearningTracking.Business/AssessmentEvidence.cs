using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Current exact applicability and approval evidence; no examination is conducted here.</summary>
public sealed record AssessmentEvidence(Guid SchoolId, Guid EnrollmentId, Guid AssessmentDefinitionVersionId, Guid ReadinessEvaluationId, Guid StudentId,
    TrainingKind Kind, string Category, bool Allowed, bool LegalApplicabilityVerified, DateTimeOffset ValidUntil);
