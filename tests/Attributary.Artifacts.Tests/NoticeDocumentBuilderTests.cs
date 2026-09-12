using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class NoticeDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string? noticeText, params ObligationKind[] obligations) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId("Apache-2.0"), "Copyright X", [], []),
            "Apache-2.0", "Copyright X", "Apache text", noticeText),
        LicensePolicy.Allow,
        obligations.Select(k => new Obligation(k, null)).ToList(),
        []);

    [Test]
    public async Task Build_ComponentWithNoticeObligation_IncludesSection()
    {
        var plans = new[] { BuildPlan("Foo", "Foo notice text", ObligationKind.Copyright, ObligationKind.LicenseText, ObligationKind.NoticeText) };

        var document = NoticeDocumentBuilder.Build(plans);

        await Assert.That(document.Sections).Count().IsEqualTo(1);
        await Assert.That(document.Sections[0].NoticeText).IsEqualTo("Foo notice text");
    }

    [Test]
    public async Task Build_ComponentWithoutNoticeObligation_IsExcluded()
    {
        var plans = new[] { BuildPlan("Foo", null, ObligationKind.Copyright, ObligationKind.LicenseText) };

        var document = NoticeDocumentBuilder.Build(plans);

        await Assert.That(document.Sections).IsEmpty();
    }
}
