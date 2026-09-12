using System.Text.Json;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SpdxCanonicalSource : ILicenseSource
{
    private static readonly Dictionary<string, string> LicenseTexts = LoadEmbeddedLicenseTexts();

    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SpdxCanonical;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        if (licenseId is null || !LicenseTexts.TryGetValue(licenseId, out var text))
            return Task.FromResult(SourceResult.Unresolved);

        var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, text, null, null, provenance));
    }

    private static Dictionary<string, string> LoadEmbeddedLicenseTexts()
    {
        var assembly = typeof(SpdxCanonicalSource).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("spdx-subset.json"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
