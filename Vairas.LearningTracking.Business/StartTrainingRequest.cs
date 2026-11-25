using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Initial immutable binding to an externally owned enrollment.</summary>
public sealed record StartTrainingRequest(Guid SchoolId, Guid EnrollmentId, Guid ProgramVersionId);
