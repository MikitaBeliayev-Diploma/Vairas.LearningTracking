using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Authoritative physical source. Actual times never represent credited teaching minutes.</summary>
public sealed record TrainingSource(Guid LessonId, Guid SchoolId, int LessonRevision, TrainingKind Kind, bool PhysicallyCompleted, bool SourceValid,
    DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd, long? GroupMembershipRevision, Guid TeacherId, Guid? EnrollmentId, Guid? StudentId, Guid? VehicleId, Guid? GroupId);
