using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>One authoritative as-of roster participant.</summary>
public sealed record Participant(Guid EnrollmentId, Guid StudentId);
