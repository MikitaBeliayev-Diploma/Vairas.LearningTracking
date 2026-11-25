using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Exact historical roster, not the current group roster substituted for it.</summary>
public sealed record Roster(Guid GroupId, Guid SchoolId, long MembershipVersion, Participant[] Participants);
