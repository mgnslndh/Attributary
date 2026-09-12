using Attributary.Artifacts;
using Attributary.Output.Text;

namespace Attributary.Output.Tests.Text;

public class TxtAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithEmbed_ShowsLicenseHeadingAndFullText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
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
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT | Copyright Foo");
        await Assert.That(result).DoesNotContain("MIT full text");
    }
}
