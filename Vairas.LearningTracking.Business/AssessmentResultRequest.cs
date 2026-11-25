using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>A performed attempt; the server derives its pass/fail outcome from the pinned scale.</summary>
public sealed record AssessmentResultRequest(Guid SchoolId, Guid EnrollmentId, Guid AssessmentDefinitionVersionId, int Mark, DateTimeOffset AssessedAt,
    Guid? ReadinessDecisionId = null, Guid? AppointmentId = null, string? Comment = null, TopicMarkInput[]? TopicMarks = null);
