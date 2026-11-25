namespace Vairas.LearningTracking.Configuration.Options;

public sealed class ServiceBusOptions
{
    public bool Enabled { get; set; }
    public string ConnectionString { get; set; } = "";
    public string TopicName { get; set; } = "vairas.learning.events.v1";
    public string SchedulingTopic { get; set; } = "vairas.scheduling.events.v1";
    public string SchoolTopic { get; set; } = "vairas.school.events.v1";
    public string SubscriptionName { get; set; } = "learning-tracking";
    public int MaxDeliveryCount { get; set; } = 5;
}
