using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Current allocation basis read directly from Scheduling.</summary>
public sealed record Allocations(Guid SchoolId, long Revision, Allocation[] Items);
