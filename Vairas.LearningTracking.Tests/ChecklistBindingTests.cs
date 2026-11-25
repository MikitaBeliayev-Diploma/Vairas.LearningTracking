using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Vairas.LearningTracking.Configuration;
using Vairas.LearningTracking.Configuration.Options;
namespace Vairas.LearningTracking.Tests;

[TestFixture, Category("Unit")]
public sealed class ChecklistBindingTests
{
    private static IConfiguration Settings(string? effectiveTo, bool includeEffectiveTo = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["Checklist:SyntheticMode"] = "true", ["Checklist:Policies:0:VersionId"] = Guid.NewGuid().ToString(), ["Checklist:Policies:0:SchoolId"] = Guid.NewGuid().ToString(),
            ["Checklist:Policies:0:Category"] = "B", ["Checklist:Policies:0:Pathway"] = "synthetic", ["Checklist:Policies:0:SourceUri"] = "https://synthetic.invalid/checklist",
            ["Checklist:Policies:0:Edition"] = "fixture-only", ["Checklist:Policies:0:VerifiedBy"] = Guid.NewGuid().ToString(), ["Checklist:Policies:0:VerifiedAt"] = "2026-01-02T00:00:00Z",
            ["Checklist:Policies:0:EffectiveFrom"] = "2026-01-01", ["Checklist:Policies:0:Synthetic"] = "true",
            ["Checklist:Policies:0:PracticalFields:0"] = "Identity", ["Checklist:Policies:0:JournalChecks:0"] = "Programme"
        };
        if (includeEffectiveTo) values["Checklist:Policies:0:EffectiveTo"] = effectiveTo;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Test]
    public void A_fully_specified_policy_binds()
    {
        var options = ChecklistOptions.Bind(Settings("2027-12-31"));
        Assert.That(options.Policies, Has.Length.EqualTo(1));
        Assert.That(options.Policies[0].EffectiveTo, Is.EqualTo(new DateOnly(2027, 12, 31)));
    }

    [TestCase("")][TestCase(null)]
    public void A_policy_the_binder_would_silently_drop_fails_startup_instead(string? effectiveTo)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ChecklistOptions.Bind(Settings(effectiveTo)));
        Assert.That(error!.Message, Does.Contain("EffectiveTo"));
        Assert.Throws<InvalidOperationException>(() => ChecklistOptions.Bind(Settings(null, includeEffectiveTo: false)));
    }

    [Test]
    public void No_configured_policies_is_valid()
    {
        Assert.That(ChecklistOptions.Bind(new ConfigurationBuilder().Build()).Policies, Is.Empty);
    }
}
