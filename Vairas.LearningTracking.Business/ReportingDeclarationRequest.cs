using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>User declaration of manual reporting. It is not an external submission acknowledgement.</summary>
public sealed record ReportingDeclarationRequest(Guid SnapshotId, string Statement);
