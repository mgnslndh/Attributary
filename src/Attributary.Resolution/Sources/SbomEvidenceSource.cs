using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SbomEvidenceSource : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.Evidence;

    public bool IsLicenseIdSpecific => false;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        var texts = component.EvidenceCopyrightTexts ?? [];
        if (texts.Count == 0)
            return Task.FromResult(SourceResult.Unresolved);

        var joined = string.Join("\n", texts.Distinct());
        var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, null, joined, null, provenance));
    }
}
