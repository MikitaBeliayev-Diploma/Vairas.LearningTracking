using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Exact School authority including programme association; never a permanent permission cache.</summary>
public sealed record LearningAuthority(Guid SchoolId, Guid ActorUserId, Guid? EnrollmentId, Guid? StudentId, string Category,
    string EnrollmentStatus, long SchoolRevision, long EnrollmentRevision, bool Allowed, bool Administrator, bool Teacher, bool Assessor,
    bool Self, bool AssignedTeacher, Guid? GroupId, Guid? ProgramVersionId, Guid? BranchId, DateTimeOffset ValidUntil, string AuthorityBasis,
    DateTimeOffset? RevisionEffectiveAt = null, string? RevisionEffectScope = null);
