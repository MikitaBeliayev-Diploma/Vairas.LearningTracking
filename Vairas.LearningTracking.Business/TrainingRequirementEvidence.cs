using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>
/// Minimal applicability answer for an additional lesson: whether the enrollment's pinned programme still has an outstanding
/// confirmed-minute requirement of that kind. No hours, records or identities are disclosed.
/// </summary>
public sealed record TrainingRequirementEvidence(Guid SchoolId, Guid EnrollmentId, TrainingKind Kind, bool Outstanding, DateTimeOffset ValidUntil);
