using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Current pinned programme and local dependency revision.</summary>
public sealed record TrainingEnrollmentResponse(Guid EnrollmentId, Guid SchoolId, Guid StudentId, Guid? ProgramVersionId, long DependencyRevision);
