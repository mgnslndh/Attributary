using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class AttributionDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string copyright, string licenseText) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), copyright, [], []),
            licenseId, copyright, licenseText, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    [Test]
    public async Task Build_AlwaysIncludesLicenseIdPerRow_RegardlessOfGrouping()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var flat = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);
        var grouped = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(flat.Rows[0].LicenseId).IsEqualTo("MIT");
        await Assert.That(grouped.Rows[0].LicenseId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Build_LicenseTextsById_AlwaysPopulatedRegardlessOfEmbedFlag()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(document.LicenseTextsById["MIT"]).IsEqualTo("MIT text");
        await Assert.That(document.EmbedLicenseText).IsFalse();
    }

    [Test]
    public async Task Build_RowsSortedByComponentName()
    {
        var plans = new[]
        {
            BuildPlan("Zeta", "MIT", "Copyright Zeta", "MIT text"),
            BuildPlan("Alpha", "MIT", "Copyright Alpha", "MIT text")
        };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);

        await Assert.That(document.Rows[0].ComponentName).IsEqualTo("Alpha");
        await Assert.That(document.Rows[1].ComponentName).IsEqualTo("Zeta");
    }
}
