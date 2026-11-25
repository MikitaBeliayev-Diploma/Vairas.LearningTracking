using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>One version-owned topic. IDs remain stable while editing a draft.</summary>
public sealed record TopicInput(Guid TopicId, string Key, string Name, string? Description = null);
