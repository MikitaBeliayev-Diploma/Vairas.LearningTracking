using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Optional assessment topic mark; never determines the overall outcome.</summary>
public sealed record TopicMarkInput(Guid AssessmentTopicId, int Mark, string? Comment = null);
