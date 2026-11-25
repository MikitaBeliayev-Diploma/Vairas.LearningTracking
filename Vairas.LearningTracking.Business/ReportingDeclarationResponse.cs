using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Retained declaration of human-controlled reporting.</summary>
public sealed record ReportingDeclarationResponse(Guid DeclarationId, Guid SnapshotId, DateTimeOffset DeclaredAt);
