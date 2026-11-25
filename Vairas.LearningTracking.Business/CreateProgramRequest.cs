using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>First programme identity within one school and category.</summary>
public sealed record CreateProgramRequest(Guid SchoolId, string CategoryCode, string Name);
