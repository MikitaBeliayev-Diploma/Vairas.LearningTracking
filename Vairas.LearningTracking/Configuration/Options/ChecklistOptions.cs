using Vairas.LearningTracking.Business;

namespace Vairas.LearningTracking.Configuration.Options;

public sealed class ChecklistOptions
{
    public bool SyntheticMode { get; set; }
    public ChecklistPolicy[] Policies { get; set; } = [];

    /// <summary>
    /// Binds "Checklist" and fails fast when a configured policy cannot be bound. The configuration binder silently drops an
    /// array element whose positional record cannot be constructed (e.g. "EffectiveTo": null becomes an empty string), which
    /// otherwise surfaces much later as VERIFIED_CHECKLIST_REQUIRED with no diagnostic.
    /// </summary>
    public static ChecklistOptions Bind(IConfiguration configuration)
    {
        var options = configuration.GetSection("Checklist").Get<ChecklistOptions>() ?? new();
        var configured = configuration.GetSection("Checklist:Policies").GetChildren().Count();
        if (configured != options.Policies.Length)
            throw new InvalidOperationException($"Only {options.Policies.Length} of {configured} Checklist:Policies entries could be bound. Every field is required; give EffectiveTo an explicit date.");
        return options;
    }
}
