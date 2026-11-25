using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Exact prospective School association decision.</summary>
public sealed record ProgrammeApplicabilityResponse(bool Allowed, Guid SchoolId, Guid EnrollmentId, Guid GroupId, Guid ProgramVersionId, DateTimeOffset ValidUntil);
