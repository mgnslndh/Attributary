namespace Attributary.Rules.Tests;

public class YamlRuleSetLoaderTests
{
    private const string Yaml = """
        defaults:
          unknownLicense:
            policy: deny
            require: [copyright, license-text]

        rules:
          - id: MIT
            policy: allow
            require: [copyright, license-text]

          - id: Apache-2.0
            policy: allow
            require:
              - copyright
              - license-text
              - notice-text: { when: upstream-notice-present }
            flags: [modification-disclosure, trademark-non-grant, patent-grant]

          - id: BSD-3-Clause
            policy: allow
            require: [copyright, license-text]
            flags: [non-endorsement]
        """;

    [Test]
    public async Task Load_UnknownLicenseDefault_IsDenyWithBaselineObligations()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        await Assert.That(ruleSet.UnknownLicenseDefault.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(ruleSet.UnknownLicenseDefault.Require).HasCount().EqualTo(2);
        await Assert.That(ruleSet.UnknownLicenseDefault.Require.Select(r => r.Kind))
            .Contains(ObligationKind.Copyright).And.Contains(ObligationKind.LicenseText);
    }

    [Test]
    public async Task Load_ApacheRule_HasConditionalNoticeObligationAndFlags()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        var apache = ruleSet.Rules.Single(r => r.IdPattern == "Apache-2.0");
        var noticeObligation = apache.Require.Single(r => r.Kind == ObligationKind.NoticeText);

        await Assert.That(apache.Policy).IsEqualTo(LicensePolicy.Allow);
        await Assert.That(noticeObligation.Condition).IsEqualTo("upstream-notice-present");
        await Assert.That(apache.Flags).Contains(ObligationFlag.ModificationDisclosure)
            .And.Contains(ObligationFlag.TrademarkNonGrant)
            .And.Contains(ObligationFlag.PatentGrant);
    }

    [Test]
    public async Task Load_Bsd3Rule_HasNonEndorsementFlagAndNoCondition()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        var bsd3 = ruleSet.Rules.Single(r => r.IdPattern == "BSD-3-Clause");

        await Assert.That(bsd3.Flags).Contains(ObligationFlag.NonEndorsement);
        await Assert.That(bsd3.Require.All(r => r.Condition is null)).IsTrue();
    }
}
