namespace Vairas.LearningTracking.Business;

/// <summary>Immutable enrollment-only evidence with current freshness kept separate.</summary>
public sealed record DocumentEvidencePayload(Guid SchoolId, Guid EnrollmentId, Guid SnapshotId, string Kind, long DependencyRevision, long CurrentDependencyRevision,
    string Status, bool IsCurrent, bool SourcesVerified, string SourceSnapshotVersion, string SchemaVersion, DateTimeOffset PreparedAt, DocumentEvidenceRow[] Rows, DocumentEvidenceCheck[] Checks,
    Guid? ProgramVersionId = null, Guid? ChecklistVersionId = null, DocumentEvidenceRuleSource[]? RuleSources = null);
