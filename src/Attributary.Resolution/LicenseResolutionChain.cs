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
            if (TryGetFlatAndAtoms(expression, out var atoms))
                return await ResolveMultiLicenseAsync(component, atoms, ct);

            var message = expression.Contains(" AND ")
                ? $"Component declares a multi-license expression '{expression}' requiring simultaneous compliance with all listed licenses; enrich the SBOM to declare a single component per license, or add explicit support for this combination."
                : $"Component declares an unresolved license expression '{expression}'; enrich the SBOM with a single license id.";
            diagnostics.Report(ResolutionDiagnostics.OrExpressionUnresolved, message, context);
            return new LicenseResolution(component, [], null, new Dictionary<string, string>(), null);
        }

        var licenseId = declared.SpdxId ?? declared.FreeTextName;
        return await ResolveSingleLicenseAsync(component, licenseId, ct);
    }

    // Only a flat top-level AND (no parens, OR, or WITH) is eligible for automatic
    // resolution — anything else keeps asking for SBOM enrichment (spec §6a).
    private static bool TryGetFlatAndAtoms(string expression, out IReadOnlyList<string> atoms)
    {
        atoms = [];
        if (expression.Contains('(')
            || expression.Contains(" OR ", StringComparison.OrdinalIgnoreCase)
            || expression.Contains(" WITH ", StringComparison.OrdinalIgnoreCase)
            || !expression.Contains(" AND ", StringComparison.Ordinal))
        {
            return false;
        }

        var split = expression.Split(" AND ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (split.Length < 2)
            return false;

        atoms = split;
        return true;
    }

    private async Task<LicenseResolution> ResolveSingleLicenseAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
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

        IReadOnlyList<string> ids = licenseId is null ? [] : [licenseId];
        var textsById = licenseId is not null && licenseText is not null
            ? new Dictionary<string, string> { [licenseId] = licenseText }
            : new Dictionary<string, string>();
        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);

        return new LicenseResolution(component, ids, copyrightText, textsById, noticeText, provenance);
    }

    private async Task<LicenseResolution> ResolveMultiLicenseAsync(SbomComponent component, IReadOnlyList<string> atomIds, CancellationToken ct)
    {
        // Copyright and notice text are properties of the component as a whole, not
        // of any one license atom -- resolve them once, id-agnostically, via the
        // full chain (spec §6a). Passing licenseId: null is fine: no shipped
        // source's copyright/notice resolution actually branches on it.
        string? copyrightText = null, noticeText = null;
        FieldProvenance? copyrightProvenance = null, noticeProvenance = null;

        foreach (var source in sources)
        {
            var result = await source.TryResolveAsync(component, null, ct);
            if (!result.Resolved) continue;

            if (copyrightText is null && result.CopyrightText is not null) { copyrightText = result.CopyrightText; copyrightProvenance = result.Provenance; }
            if (noticeText is null && result.NoticeText is not null) { noticeText = result.NoticeText; noticeProvenance = result.Provenance; }

            if (copyrightText is not null && noticeText is not null) break;
        }

        // License text: only id-specific sources are safe to ask per atom (see
        // ILicenseSource.IsLicenseIdSpecific) -- a package-level source (NuGet/
        // GitHub/license-URL) would hand back the same blob for every atom, which
        // is wrong, not just imprecise.
        var idSpecificSources = sources.Where(s => s.IsLicenseIdSpecific).ToList();
        var textsById = new Dictionary<string, string>();
        FieldProvenance? licenseProvenance = null;

        foreach (var atomId in atomIds)
        {
            foreach (var source in idSpecificSources)
            {
                var result = await source.TryResolveAsync(component, atomId, ct);
                if (result.Resolved && result.LicenseText is not null)
                {
                    textsById[atomId] = result.LicenseText;
                    licenseProvenance ??= result.Provenance;
                    break;
                }
            }
        }

        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);
        return new LicenseResolution(component, atomIds, copyrightText, textsById, noticeText, provenance);
    }
}
