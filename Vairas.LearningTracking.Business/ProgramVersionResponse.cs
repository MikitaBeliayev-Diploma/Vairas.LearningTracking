using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Programme version and complete curriculum for review and publication.</summary>
public sealed record ProgramVersionResponse(Guid ProgramVersionId, Guid ProgramId, Guid SchoolId, int Version, ProgramStatus Status, long Revision, ProgramVersionRequest Curriculum);
