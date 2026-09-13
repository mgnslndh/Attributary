using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Text;
using Attributary.Rules;

namespace Attributary.Output.Tests.Text;

public class TxtComplianceReportWriterTests
{
    [Test]
    public async Task Render_IncludesEntriesAndFlaggedForReviewSections()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["GPL-3.0-only"], ResolutionSourceStrategy.SpdxCanonical, [ObligationKind.Copyright])],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", ["GPL-3.0-only"], [ObligationFlag.SourceOffer], LicensePolicy.Warn)]);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - GPL-3.0-only (source: SpdxCanonical)");
        await Assert.That(result).Contains("Foo 1.0.0 - GPL-3.0-only - policy: Warn, flags: SourceOffer");
    }

    [Test]
    public async Task Render_MultiLicenseEntry_JoinsIdsWithAnd()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["MIT", "Apache-2.0"], ResolutionSourceStrategy.SpdxCanonical, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - MIT AND Apache-2.0 (source: SpdxCanonical)");
    }

    [Test]
    public async Task Render_MultiLicenseFlaggedForReview_JoinsIdsWithAnd()
    {
        var document = new ComplianceReportDocument(
            Entries: [],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", ["MIT", "Apache-2.0"], [ObligationFlag.SourceOffer], LicensePolicy.Warn)]);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - MIT AND Apache-2.0 - policy: Warn, flags: SourceOffer");
    }
}
