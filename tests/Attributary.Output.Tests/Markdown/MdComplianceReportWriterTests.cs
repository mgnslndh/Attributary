using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Markdown;
using Attributary.Rules;

namespace Attributary.Output.Tests.Markdown;

public class MdComplianceReportWriterTests
{
    [Test]
    public async Task Render_ProducesTablesForEntriesAndFlags()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", "MIT", [ObligationFlag.NonEndorsement], LicensePolicy.Allow)]);
        var writer = new MdComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("# Compliance Report");
        await Assert.That(result).Contains("| Foo | 1.0.0 | MIT |");
        await Assert.That(result).Contains("NonEndorsement");
    }
}
