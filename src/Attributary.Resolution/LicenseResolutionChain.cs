using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution;

public sealed class LicenseResolutionChain(IReadOnlyList<ILicenseSource> sources) : ILicenseResolutionChain
{
    public async Task<LicenseResolution> ResolveAsync(SbomComponent component, IDiagnosticSink diagnostics, CancellationToken ct)
    {
        var context = $"{component.Name} {component.Version}";
        var declared = component.DeclaredLicense;

        if (declared.SpdxExpression is { } expression)
        {
            diagnostics.Report(ResolutionDiagnostics.OrExpressionUnresolved,
                $"Component declares an unresolved license expression '{expression}'; enrich the SBOM with a single license id.", context);
            return new LicenseResolution(component, null, null, null, null);
        }

        var licenseId = declared.SpdxId ?? declared.FreeTextName;

        string? licenseText = null, copyrightText = null, noticeText = null;
        FieldProvenance? licenseProvenance = null, copyrightProvenance = null, noticeProvenance = null;

        foreach (var source in sources)
        {
            var result = await source.TryResolveAsync(component, licenseId, ct);
            if (!result.Resolved) continue;

            if (licenseText is null && result.LicenseText is not null) { licenseText = result.LicenseText; licenseProvenance = result.Provenance; }
            if (copyrightText is null && result.CopyrightText is not null) { copyrightText = result.CopyrightText; copyrightProvenance = result.Provenance; }
            if (noticeText is null && result.NoticeText is not null) { noticeText = result.NoticeText; noticeProvenance = result.Provenance; }

            if (licenseText is not null && copyrightText is not null) break;
        }

        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);
        return new LicenseResolution(component, licenseId, copyrightText, licenseText, noticeText, provenance);
    }
}
