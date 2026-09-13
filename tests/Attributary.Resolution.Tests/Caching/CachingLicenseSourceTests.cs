using Attributary.Domain;
using Attributary.Resolution.Caching;

namespace Attributary.Resolution.Tests.Caching;

file sealed class CountingSource(SourceResult result, ResolutionSourceStrategy strategy) : ILicenseSource
{
    public int CallCount { get; private set; }
    public ResolutionSourceStrategy Strategy => strategy;
    public bool IsLicenseIdSpecific => strategy == ResolutionSourceStrategy.SpdxCanonical;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

public class CachingLicenseSourceTests
{
    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "attributary-cache-tests", Guid.NewGuid().ToString());

    [Test]
    public async Task TryResolveAsync_SecondCallForSameSpdxId_HitsCacheNotInnerSource()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var inner = new CountingSource(new SourceResult(true, "MIT text", null, null, provenance), ResolutionSourceStrategy.SpdxCanonical);
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var caching = new CachingLicenseSource(inner, store);
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);

        var first = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);
        var second = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(first.LicenseText).IsEqualTo("MIT text");
        await Assert.That(second.LicenseText).IsEqualTo("MIT text");
        await Assert.That(inner.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task TryResolveAsync_CacheHit_MarksProvenanceFromCache()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var inner = new CountingSource(new SourceResult(true, "MIT text", null, null, provenance), ResolutionSourceStrategy.SpdxCanonical);
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var caching = new CachingLicenseSource(inner, store);
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);

        await caching.TryResolveAsync(component, "MIT", CancellationToken.None);
        var second = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(second.Provenance!.FromCache).IsTrue();
    }
}
