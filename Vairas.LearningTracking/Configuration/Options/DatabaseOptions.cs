namespace Vairas.LearningTracking.Configuration.Options;

/// <summary>Bound from "ConnectionStrings": restricted runtime role plus the separate delivery/reconciliation role.</summary>
public sealed class DatabaseOptions
{
    public string LearningTracking { get; set; } = "";
    public string Delivery { get; set; } = "";
}
