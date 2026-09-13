// tests/Attributary.Artifacts.Tests/ComplianceReportDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class ComplianceReportDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, LicensePolicy policy, IReadOnlyList<ObligationFlag> flags) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId("GPL-3.0-only"), "Copyright X", [], []),
            ["GPL-3.0-only"], "Copyright X",
            new Dictionary<string, string> { ["GPL-3.0-only"] = "GPL text" }, null,
            new ResolutionProvenance(new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false), null, null)),
        policy,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        flags);

    [Test]
    public async Task Build_EveryPlan_HasAnEntryWithSatisfiedObligations()
    {
        var plans = new[] { BuildPlan("Foo", LicensePolicy.Allow, []) };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.Entries).Count().IsEqualTo(1);
        await Assert.That(report.Entries[0].SatisfiedObligations).Contains(ObligationKind.Copyright);
        await Assert.That(report.Entries[0].LicenseTextSource).IsEqualTo(ResolutionSourceStrategy.SpdxCanonical);
    }

    [Test]
    public async Task Build_PlanWithFlagsOrNonAllowPolicy_AppearsInFlaggedForReview()
    {
        var plans = new[]
        {
            BuildPlan("Foo", LicensePolicy.Warn, [ObligationFlag.SourceOffer, ObligationFlag.CopyleftStrong]),
            BuildPlan("Bar", LicensePolicy.Allow, [])
        };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.FlaggedForReview).Count().IsEqualTo(1);
        await Assert.That(report.FlaggedForReview[0].ComponentName).IsEqualTo("Foo");
        await Assert.That(report.FlaggedForReview[0].Flags).Contains(ObligationFlag.SourceOffer);
    }

    [Test]
    public async Task Build_MultiLicenseComponent_EntryCarriesAllLicenseIds()
    {
        var plans = new[]
        {
            new ObligationPlan(
                new LicenseResolution(
                    new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromExpression("MIT AND Apache-2.0"), "Copyright X", [], []),
                    ["MIT", "Apache-2.0"], "Copyright X",
                    new Dictionary<string, string> { ["MIT"] = "MIT text" }, null),
                LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [])
        };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.Entries[0].LicenseIds).Contains("MIT").And.Contains("Apache-2.0");
    }
}
