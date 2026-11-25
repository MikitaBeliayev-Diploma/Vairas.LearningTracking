using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Exact optional calendar allocation; this does not prove an assessment was performed.</summary>
public sealed record AssessmentAppointment(Guid SchoolId, Guid EnrollmentId, Guid DefinitionVersionId, Guid TeacherId, string Status, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
