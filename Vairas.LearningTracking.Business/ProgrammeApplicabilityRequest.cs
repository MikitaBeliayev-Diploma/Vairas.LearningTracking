using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>School checks a prospective association without requiring it already to have been committed.</summary>
public sealed record ProgrammeApplicabilityRequest(Guid SchoolId, Guid EnrollmentId, Guid GroupId, Guid ProgramVersionId);
