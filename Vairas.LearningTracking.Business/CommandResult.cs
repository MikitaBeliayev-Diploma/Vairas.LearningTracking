using Vairas.LearningTracking.Domain;

namespace Vairas.LearningTracking.Business;

/// <summary>Internal command outcome; the HTTP adapter returns Value with appropriate replay semantics.</summary>
public sealed record CommandResult<T>(T Value, bool Replayed);
