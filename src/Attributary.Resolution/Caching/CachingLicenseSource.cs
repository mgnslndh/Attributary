using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed class CachingLicenseSource(ILicenseSource inner, ILicenseCacheStore store) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => inner.Strategy;

    public async Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        var discriminator = inner.Strategy == ResolutionSourceStrategy.SpdxCanonical
            ? licenseId
            : component.Purl is not null ? $"{component.Purl}@{component.Version}" : null;

        if (discriminator is null)
            return await inner.TryResolveAsync(component, licenseId, ct);

        var key = new CacheKey(inner.Strategy, discriminator);
        var cached = store.TryGet(key);
        if (cached is not null)
        {
            var cachedProvenance = new FieldProvenance(inner.Strategy, cached.SourceUrl, cached.FetchedAtUtc, FromCache: true);
            return new SourceResult(true, cached.Content, null, null, cachedProvenance);
        }

        var result = await inner.TryResolveAsync(component, licenseId, ct);
        if (result.Resolved && result.LicenseText is not null)
        {
            var sha = FileSystemLicenseCacheStore.ComputeSha256(result.LicenseText);
            store.Put(key, new CacheEntry(result.LicenseText, sha, result.Provenance?.SourceUrl, DateTimeOffset.UtcNow));
        }

        return result;
    }
}
