using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Current authenticated actor. Profile identities are resolved through User Management.</summary>
public sealed record LearningActor(Guid IdentityId, Guid UserId, Guid? StudentId, Guid? TeacherId, DateTimeOffset ValidUntil, string CorrelationId, Channel Channel = Channel.Web);
