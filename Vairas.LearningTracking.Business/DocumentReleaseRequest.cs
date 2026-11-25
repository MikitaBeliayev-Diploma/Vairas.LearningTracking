namespace Vairas.LearningTracking.Business;

/// <summary>Bind a publication or current-use decision to exact evidence and artifact bytes.</summary>
public sealed record DocumentReleaseRequest(Guid SchoolId, Guid EnrollmentId, Guid SnapshotId, long ExpectedDependencyRevision, Guid DocumentVersionId,
    Guid ActorUserId, string Kind, string ArtifactDigest, string Purpose, Guid? TeacherId = null);
