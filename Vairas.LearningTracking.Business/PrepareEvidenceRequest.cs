using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Prepare an immutable journal or completion report evidence evaluation.</summary>
public sealed record PrepareEvidenceRequest(EvidenceKind Kind);
