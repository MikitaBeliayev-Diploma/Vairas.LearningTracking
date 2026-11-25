using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Only current confirmed practical versions for an authorized document consumer.</summary>
public sealed record ConfirmedPracticalRecordsResponse(Guid EnrollmentId, string SnapshotVersion, ConfirmedPracticalRecord[] Records);
