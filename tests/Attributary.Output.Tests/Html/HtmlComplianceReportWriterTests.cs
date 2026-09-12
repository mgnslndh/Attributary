using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Html;
using Attributary.Rules;

namespace Attributary.Output.Tests.Html;

public class HtmlComplianceReportWriterTests
{
    [Test]
    public async Task Render_ProducesHtmlTablesForEntriesAndFlags()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new HtmlComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("<h1>Compliance Report</h1>");
        await Assert.That(result).Contains("<td>Foo</td>");
    }
}
