using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Configuration.Options;

namespace Vairas.LearningTracking.Configuration;


public sealed class ChecklistRegistry : IChecklistRegistry
{
    private readonly ChecklistPolicy[] policies;
    private readonly bool synthetic;
    public ChecklistRegistry(ChecklistOptions options, TimeProvider clock)
    {
        policies = options.Policies; synthetic = options.SyntheticMode;
        if (policies.Select(x => x.VersionId).Distinct().Count() != policies.Length || policies.Any(p => p.VersionId == Guid.Empty || p.SchoolId == Guid.Empty || p.VerifiedBy == Guid.Empty || p.VerifiedAt == default ||
            p.VerifiedAt > clock.GetUtcNow() || string.IsNullOrWhiteSpace(p.Category) || string.IsNullOrWhiteSpace(p.Pathway) || string.IsNullOrWhiteSpace(p.Edition) ||
            !Uri.TryCreate(p.SourceUri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || p.PracticalFields is null || p.JournalChecks is null || p.PracticalFields.Length == 0 || p.JournalChecks.Length == 0 ||
            p.PracticalFields.Distinct().Count() != p.PracticalFields.Length || p.JournalChecks.Distinct().Count() != p.JournalChecks.Length || p.EffectiveTo < p.EffectiveFrom))
            throw new InvalidOperationException("Invalid external checklist configuration.");
    }
    public ChecklistPolicy? Find(Guid version, Guid school, string category, string pathway, DateOnly date) => policies.SingleOrDefault(p => p.VersionId == version && p.SchoolId == school &&
        p.Category == category && p.Pathway == pathway && p.EffectiveFrom <= date && !(p.EffectiveTo < date) && (!p.Synthetic || synthetic));
}
