using Attributary.Artifacts;
using Attributary.Output.Html;

namespace Attributary.Output.Tests.Html;

public class HtmlAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithoutEmbed_LinksToLicensesFolder()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new HtmlAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("<h2>MIT</h2>");
        await Assert.That(result).Contains("<a href=\"LICENSES/MIT.txt\">");
    }

    [Test]
    public async Task Render_EncodesHtmlSpecialCharactersInCopyright()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright <Foo & Co>")],
            LicenseTextsById: new Dictionary<string, string>(),
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new HtmlAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Copyright &lt;Foo &amp; Co&gt;");
    }
}
