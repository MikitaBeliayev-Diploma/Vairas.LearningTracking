using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Current minimal assessment outcome. Marks, journal details and comments are deliberately absent.</summary>
public sealed record NotificationSourceResponse(Guid SchoolId, Guid SourceId, long Revision, string Kind, string Status,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt, Guid[] EnrollmentIds, Guid? TeacherId, DateTimeOffset? AssessedAt, string? Outcome);
