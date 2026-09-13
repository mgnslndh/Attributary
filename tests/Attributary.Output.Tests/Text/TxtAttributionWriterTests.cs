using Attributary.Artifacts;
using Attributary.Output.Text;

namespace Attributary.Output.Tests.Text;

public class TxtAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithEmbed_ShowsLicenseHeadingAndFullText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: true);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("License: MIT");
        await Assert.That(result).Contains("Foo 1.0.0 - Copyright Foo");
        await Assert.That(result).Contains("MIT full text");
    }

    [Test]
    public async Task Render_FlatWithoutEmbed_ShowsRowsButNoLicenseText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT | Copyright Foo");
        await Assert.That(result).DoesNotContain("MIT full text");
    }

    [Test]
    public async Task Render_MultiLicenseRow_FlatModeJoinsIdsWithAnd()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT AND Apache-2.0 | Copyright Foo");
    }

    [Test]
    public async Task Render_MultiLicenseRow_GroupedModeListsComponentUnderEachLicenseHeading()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("License: MIT");
        await Assert.That(result).Contains("License: Apache-2.0");
        var fooOccurrences = result.Split("Foo 1.0.0").Length - 1;
        await Assert.That(fooOccurrences).IsEqualTo(2);
    }
}
