using Attributary.Domain;

namespace Attributary.Rules.Tests;

public class ObligationPlanBuilderTests
{
    private static SbomComponent BuildComponent(string licenseId) => new(
        Name: "Foo", Version: "1.0.0", Purl: null,
        DeclaredLicense: LicenseExpression.FromId(licenseId),
        RawCopyright: "Copyright Foo Inc.",
        ExternalReferences: [], Evidence: []);

    private static RuleSet BuildRuleSet() => new(
        UnknownLicenseDefault: new LicenseRule("*", LicensePolicy.Deny,
            [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
        Rules:
        [
            new LicenseRule("Apache-2.0", LicensePolicy.Allow,
                [
                    new Obligation(ObligationKind.Copyright, null),
                    new Obligation(ObligationKind.LicenseText, null),
                    new Obligation(ObligationKind.NoticeText, "upstream-notice-present")
                ],
                [ObligationFlag.ModificationDisclosure])
        ]);

    [Test]
    public async Task Build_ConditionResolvesTrue_KeepsConditionalObligation()
    {
        var resolution = new LicenseResolution(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", "Notice text");
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = true });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).Contains(ObligationKind.NoticeText);
        await Assert.That(plan.Policy).IsEqualTo(LicensePolicy.Allow);
    }

    [Test]
    public async Task Build_ConditionResolvesFalse_DropsConditionalObligationSilently()
    {
        var resolution = new LicenseResolution(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", null);
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = false });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).DoesNotContain(ObligationKind.NoticeText);
    }
}
