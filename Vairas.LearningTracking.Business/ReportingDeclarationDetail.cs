using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>An internal declaration that reporting was done manually outside the system; never an external acknowledgement.</summary>
public sealed record ReportingDeclarationDetail(Guid DeclarationId, Guid SnapshotId, Guid ActorUserId, DateTimeOffset DeclaredAt, string Statement);
