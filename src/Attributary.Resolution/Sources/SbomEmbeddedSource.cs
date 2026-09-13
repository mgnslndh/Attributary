using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SbomEmbeddedSource : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        if (component.RawCopyright is null && component.EmbeddedLicenseText is null)
            return Task.FromResult(SourceResult.Unresolved);

        var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, component.EmbeddedLicenseText, component.RawCopyright, null, provenance));
    }
}
