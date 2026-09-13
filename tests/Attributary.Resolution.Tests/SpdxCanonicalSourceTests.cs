using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

public class SpdxCanonicalSourceTests
{
    [Test]
    public async Task TryResolveAsync_KnownSpdxId_ReturnsLicenseText()
    {
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new SpdxCanonicalSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.LicenseText).Contains("MIT License");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.SpdxCanonical);
    }

    [Test]
    public async Task TryResolveAsync_UnknownSpdxId_IsUnresolved()
    {
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("Some-Obscure-License"), null, [], []);
        var source = new SpdxCanonicalSource();

        var result = await source.TryResolveAsync(component, "Some-Obscure-License", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task IsLicenseIdSpecific_IsTrue()
    {
        var source = new SpdxCanonicalSource();
        await Assert.That(source.IsLicenseIdSpecific).IsTrue();
    }
}
