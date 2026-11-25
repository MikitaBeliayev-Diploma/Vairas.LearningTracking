using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Immutable evidence with a current release assessment; historical Ready alone grants no release.</summary>
public sealed record EvidenceResponse(Guid SnapshotId, Guid EnrollmentId, Guid ProgramVersionId, EvidenceKind Kind, EvidenceStatus Status,
    long DependencyRevision, Guid? ChecklistVersionId, bool Current, EvidenceCheckResult[] Checks, Guid SchoolId);
