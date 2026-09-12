namespace Attributary.Rules.Tests;

public class RuleMatcherTests
{
    private static RuleSet BuildRuleSet() => new(
        UnknownLicenseDefault: new LicenseRule("*", LicensePolicy.Deny,
            [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
        Rules:
        [
            new LicenseRule("MIT", LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
            new LicenseRule("GPL-3.0-*", LicensePolicy.Warn,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [ObligationFlag.SourceOffer, ObligationFlag.CopyleftStrong])
        ]);

    [Test]
    public async Task Match_ExactId_ReturnsMatchingRule()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("MIT", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Allow);
    }

    [Test]
    public async Task Match_GlobPattern_MatchesVersionFamily()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("GPL-3.0-only", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(rule.Flags).Contains(ObligationFlag.CopyleftStrong);
    }

    [Test]
    public async Task Match_NoRuleFound_ReturnsUnknownDefault()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("Some-Obscure-License", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Deny);
    }

    [Test]
    public async Task MatchExpression_TwoLicenses_UnionsObligationsAndTakesMostRestrictivePolicy()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.MatchExpression(["MIT", "GPL-3.0-only"], BuildRuleSet());

        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(rule.Flags).Contains(ObligationFlag.SourceOffer);
        await Assert.That(rule.Require.Select(r => r.Kind).Distinct()).Count().IsEqualTo(2);
    }
}
