using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Business;

/// <summary>Half-open UTC planning window.</summary>
public sealed record TimeWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
