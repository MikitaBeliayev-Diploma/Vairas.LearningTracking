namespace Vairas.LearningTracking.Domain;

public sealed class RuleViolation(string code) : Exception(code) { public string Code { get; } = code; }
