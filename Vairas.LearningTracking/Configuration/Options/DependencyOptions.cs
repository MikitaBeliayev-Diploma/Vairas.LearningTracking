namespace Vairas.LearningTracking.Configuration.Options;

public sealed class DependencyOptions
{
    public string AuthBaseUrl { get; set; } = "https://localhost:7100/";
    public string UserManagementBaseUrl { get; set; } = "https://localhost:7001/";
    public string SchoolBaseUrl { get; set; } = "https://localhost:7200/";
    public string VehicleBaseUrl { get; set; } = "";
    public string SchedulingBaseUrl { get; set; } = "";
    public string ChatBaseUrl { get; set; } = "";
    public string ClientId { get; set; } = "learning-tracking";
    public string ClientSecret { get; set; } = "";
    public string Scope { get; set; } = "identity.validate profiles.status.read profiles.binding.read school.learning.read school.participants.read scheduling.source.read";
}
