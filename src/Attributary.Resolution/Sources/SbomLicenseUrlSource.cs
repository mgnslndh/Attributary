using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SbomLicenseUrlSource(HttpClient httpClient) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.LicenseUrl;

    public async Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        var licenseRef = component.ExternalReferences.FirstOrDefault(r => r.Type == ExternalReferenceType.License);
        if (licenseRef is null)
            return SourceResult.Unresolved;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(licenseRef.Url, ct);
        }
        catch (HttpRequestException)
        {
            return SourceResult.Unresolved;
        }

        if (!response.IsSuccessStatusCode)
            return SourceResult.Unresolved;

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not null && mediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return SourceResult.Unresolved;

        var bodyText = await response.Content.ReadAsStringAsync(ct);
        var provenance = new FieldProvenance(Strategy, licenseRef.Url, DateTimeOffset.UtcNow, FromCache: false);
        return new SourceResult(true, bodyText, null, null, provenance);
    }
}
