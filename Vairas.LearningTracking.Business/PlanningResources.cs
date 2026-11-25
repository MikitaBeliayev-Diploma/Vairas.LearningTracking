using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>School metadata required to construct bounded candidates without calling back to Scheduling.</summary>
public sealed record PlanningResources(Guid SchoolId, string Category, Guid BranchId, Guid[] TeacherIds, Participant[] Participants, DateTimeOffset ValidUntil);
