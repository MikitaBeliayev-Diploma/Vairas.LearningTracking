using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Existing allocation used only to avoid proposing the same curriculum demand twice.</summary>
public sealed record Allocation(Guid LessonId, TrainingKind Kind, Guid? EnrollmentId, Guid? GroupId, int Minutes, string Status);
