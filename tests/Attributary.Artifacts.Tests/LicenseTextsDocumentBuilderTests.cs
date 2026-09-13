using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class LicenseTextsDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string licenseText, params ObligationKind[] obligations) => new(
        LicenseResolution.ForSingleLicense(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), "Copyright X", [], []),
            licenseId, "Copyright X", licenseText, null),
        LicensePolicy.Allow,
        obligations.Select(k => new Obligation(k, null)).ToList(),
        []);

    [Test]
    public async Task Build_TwoComponentsSharingLicense_DedupesIntoOneEntry()
    {
        var plans = new[]
        {
            BuildPlan("Foo", "MIT", "MIT text", ObligationKind.Copyright, ObligationKind.LicenseText),
            BuildPlan("Bar", "MIT", "MIT text", ObligationKind.Copyright, ObligationKind.LicenseText)
        };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).Count().IsEqualTo(1);
        await Assert.That(document.Licenses[0].LicenseId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Build_ComponentWithoutLicenseTextObligation_IsExcluded()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "MIT text", ObligationKind.Copyright) };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).IsEmpty();
    }

    [Test]
    public async Task Build_MultiLicenseComponent_ContributesOneEntryPerAtom()
    {
        var plans = new[]
        {
            new ObligationPlan(
                new LicenseResolution(
                    new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromExpression("MIT AND Apache-2.0"), "Copyright X", [], []),
                    ["MIT", "Apache-2.0"], "Copyright X",
                    new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" }, null),
                LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [])
        };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).Count().IsEqualTo(2);
        await Assert.That(document.Licenses.Select(e => e.LicenseId)).Contains("MIT").And.Contains("Apache-2.0");
    }
}
