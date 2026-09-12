using System.Text.Json;
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Json;
using Attributary.Rules;

namespace Attributary.Output.Tests.Json;

public class JsonComplianceReportWriterTests
{
    [Test]
    public async Task Render_SerializesEntriesWithEnumsAsStrings()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new JsonComplianceReportWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("entries")[0].GetProperty("licenseTextSource").GetString()).IsEqualTo("SbomEmbedded");
    }
}
