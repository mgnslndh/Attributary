using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class LicenseTextsDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string licenseText, params ObligationKind[] obligations) => new(
        new LicenseResolution(
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
}
