namespace Attributary.Rules.Tests;

public class DefaultRuleSetProviderTests
{
    [Test]
    public async Task Load_ReturnsAllBundledLicenses()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();

        await Assert.That(ruleSet.UnknownLicenseDefault.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(ruleSet.Rules.Select(r => r.IdPattern)).Contains("MIT").And.Contains("Apache-2.0");
    }

    [Test]
    public async Task Load_JsonLicense_IsDeniedAndFlaggedNonOsiApproved()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();
        var json = ruleSet.Rules.Single(r => r.IdPattern == "JSON");

        await Assert.That(json.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(json.Flags).Contains(ObligationFlag.NonOsiApproved);
    }

    [Test]
    public async Task Load_Gpl3Family_IsWarnWithSourceOfferAndCopyleftStrong()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();
        var gpl3 = ruleSet.Rules.Single(r => r.IdPattern == "GPL-3.0-*");

        await Assert.That(gpl3.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(gpl3.Flags).Contains(ObligationFlag.SourceOffer).And.Contains(ObligationFlag.CopyleftStrong);
    }

    [Test]
    public async Task LoadRawYaml_ContainsDefaultsAndRulesKeys()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var yaml = provider.LoadRawYaml();

        await Assert.That(yaml).Contains("defaults:").And.Contains("rules:").And.Contains("MIT");
    }
}
