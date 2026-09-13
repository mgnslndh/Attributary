using System.Text.Json;
using Attributary.Artifacts;
using Attributary.Output.Json;

namespace Attributary.Output.Tests.Json;

public class JsonAttributionWriterTests
{
    [Test]
    public async Task Render_EmbedTrue_IncludesFullLicenseTextInPayload()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: true);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("licenses").GetProperty("MIT").GetString()).IsEqualTo("MIT full text");
    }

    [Test]
    public async Task Render_EmbedFalse_LicenseValueIsNull()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("licenses").GetProperty("MIT").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Render_MultiLicenseRow_EmitsLicenseIdsAsJsonArray()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: false, EmbedLicenseText: true);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        var ids = doc.RootElement.GetProperty("components")[0].GetProperty("licenseIds")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        await Assert.That(ids).Contains("MIT").And.Contains("Apache-2.0");
    }
}
