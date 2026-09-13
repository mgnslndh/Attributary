using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

public class SbomEvidenceSourceTests
{
    [Test]
    public async Task TryResolveAsync_EvidenceCopyrightTextsPresent_ResolvesJoinedCopyright()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: null, ExternalReferences: [], Evidence: [],
            EvidenceCopyrightTexts: ["Copyright (c) 2020 Foo Inc.", "Copyright (c) 2021 Bar Inc."]);
        var source = new SbomEvidenceSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.CopyrightText).IsEqualTo("Copyright (c) 2020 Foo Inc.\nCopyright (c) 2021 Bar Inc.");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.Evidence);
    }

    [Test]
    public async Task TryResolveAsync_NoEvidenceCopyrightTexts_IsUnresolved()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: null, ExternalReferences: [], Evidence: [],
            EvidenceCopyrightTexts: null);
        var source = new SbomEvidenceSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
