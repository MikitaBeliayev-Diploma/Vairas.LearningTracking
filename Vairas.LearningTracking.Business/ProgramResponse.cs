using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Immutable programme identity and optimistic revision.</summary>
public sealed record ProgramResponse(Guid ProgramId, Guid SchoolId, string CategoryCode, string Name, long Revision);
