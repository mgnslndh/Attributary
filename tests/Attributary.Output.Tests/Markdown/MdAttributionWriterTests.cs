using Attributary.Artifacts;
using Attributary.Output.Markdown;

namespace Attributary.Output.Tests.Markdown;

public class MdAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithoutEmbed_LinksToLicensesFolder()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new MdAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("## MIT");
        await Assert.That(result).Contains("[View full license text](LICENSES/MIT.txt)");
        await Assert.That(result).DoesNotContain("MIT full text");
    }

    [Test]
    public async Task Render_FlatWithEmbed_UsesTableWithLinkedLicenseColumn()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: true);
        var writer = new MdAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("| Foo | 1.0.0 | [MIT](LICENSES/MIT.txt) | Copyright Foo |");
    }

    [Test]
    public async Task Render_FlatMultiLicense_JoinsPerAtomLinksWithAnd()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string>
            {
                ["MIT"] = "MIT full text",
                ["Apache-2.0"] = "Apache full text"
            },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new MdAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("[MIT](LICENSES/MIT.txt) AND [Apache-2.0](LICENSES/Apache-2.0.txt)");
    }
}
