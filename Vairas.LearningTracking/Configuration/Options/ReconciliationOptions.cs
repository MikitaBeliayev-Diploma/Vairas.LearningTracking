namespace Vairas.LearningTracking.Configuration.Options;

public sealed class ReconciliationOptions
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
}
