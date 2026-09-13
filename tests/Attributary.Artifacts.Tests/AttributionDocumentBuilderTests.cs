using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class AttributionDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string copyright, string licenseText) => new(
        LicenseResolution.ForSingleLicense(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), copyright, [], []),
            licenseId, copyright, licenseText, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    private static ObligationPlan BuildMultiLicensePlan(string name, IReadOnlyList<string> licenseIds, string copyright, IReadOnlyDictionary<string, string> licenseTexts) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromExpression(string.Join(" AND ", licenseIds)), copyright, [], []),
            licenseIds, copyright, licenseTexts, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    [Test]
    public async Task Build_AlwaysIncludesLicenseIdsPerRow_RegardlessOfGrouping()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var flat = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);
        var grouped = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(flat.Rows[0].LicenseIds).Count().IsEqualTo(1);
        await Assert.That(flat.Rows[0].LicenseIds[0]).IsEqualTo("MIT");
        await Assert.That(grouped.Rows[0].LicenseIds[0]).IsEqualTo("MIT");
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

    [Test]
    public async Task Build_MultiLicenseComponent_RowCarriesAllLicenseIdsAndBothTextsAreInDictionary()
    {
        var plans = new[]
        {
            BuildMultiLicensePlan("Foo", ["MIT", "Apache-2.0"], "Copyright Foo",
                new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" })
        };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);

        await Assert.That(document.Rows[0].LicenseIds).Contains("MIT").And.Contains("Apache-2.0");
        await Assert.That(document.LicenseTextsById["MIT"]).IsEqualTo("MIT text");
        await Assert.That(document.LicenseTextsById["Apache-2.0"]).IsEqualTo("Apache text");
    }
}
