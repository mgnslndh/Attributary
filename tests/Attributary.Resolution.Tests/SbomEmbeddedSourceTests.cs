using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

public class SbomEmbeddedSourceTests
{
    [Test]
    public async Task TryResolveAsync_CopyrightPresentOnComponent_ResolvesCopyright()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: "Copyright (c) 2020 Foo Inc.",
            ExternalReferences: [], Evidence: []);
        var source = new SbomEmbeddedSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.CopyrightText).IsEqualTo("Copyright (c) 2020 Foo Inc.");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.SbomEmbedded);
    }

    [Test]
    public async Task TryResolveAsync_NoCopyright_IsUnresolved()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: null, ExternalReferences: [], Evidence: []);
        var source = new SbomEmbeddedSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
