namespace Attributary.Rules.Tests;

public class RuleSetMergerTests
{
    private static LicenseRule Rule(string id, LicensePolicy policy) => new(id, policy, [new Obligation(ObligationKind.LicenseText, null)], []);

    [Test]
    public async Task Merge_OverrideRule_ReplacesBaselineRuleWithSameId()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow)]);
        var overrides = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Warn)]);

        var merged = RuleSetMerger.Merge(baseline, overrides);

        await Assert.That(merged.Rules.Single(r => r.IdPattern == "MIT").Policy).IsEqualTo(LicensePolicy.Warn);
    }

    [Test]
    public async Task Merge_BaselineRuleNotInOverride_PassesThroughUnchanged()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow), Rule("Apache-2.0", LicensePolicy.Allow)]);
        var overrides = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Warn)]);

        var merged = RuleSetMerger.Merge(baseline, overrides);

        await Assert.That(merged.Rules.Select(r => r.IdPattern)).Contains("Apache-2.0");
    }

    [Test]
    public async Task Merge_NullOverride_ReturnsBaselineUnchanged()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow)]);

        var merged = RuleSetMerger.Merge(baseline, null);

        await Assert.That(merged).IsEqualTo(baseline);
    }
}
