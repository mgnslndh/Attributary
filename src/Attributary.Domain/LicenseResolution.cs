namespace Attributary.Domain;

public sealed record LicenseResolution(
    SbomComponent Component,
    IReadOnlyList<string> ResolvedLicenseIds,
    string? CopyrightText,
    IReadOnlyDictionary<string, string> LicenseTextsByLicenseId,
    string? NoticeText,
    ResolutionProvenance? Provenance = null)
{
    public static LicenseResolution ForSingleLicense(
        SbomComponent component,
        string? licenseId,
        string? copyrightText,
        string? licenseText,
        string? noticeText,
        ResolutionProvenance? provenance = null)
    {
        IReadOnlyList<string> ids = licenseId is null ? [] : [licenseId];
        var texts = licenseId is not null && licenseText is not null
            ? new Dictionary<string, string> { [licenseId] = licenseText }
            : new Dictionary<string, string>();
        return new LicenseResolution(component, ids, copyrightText, texts, noticeText, provenance);
    }
}
