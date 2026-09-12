using Attributary.Rules;

namespace Attributary.Artifacts;

public static class AttributionDocumentBuilder
{
    public static AttributionDocument Build(IReadOnlyList<ObligationPlan> plans, bool groupByLicense, bool embedLicenseText)
    {
        var rows = plans
            .Select(p => new AttributionRow(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseId ?? "UNKNOWN",
                p.Resolution.CopyrightText ?? ""))
            .OrderBy(r => r.ComponentName, StringComparer.Ordinal)
            .ToList();

        var licenseTexts = plans
            .Where(p => p.Resolution.ResolvedLicenseId is not null && p.Resolution.LicenseText is not null)
            .GroupBy(p => p.Resolution.ResolvedLicenseId!)
            .ToDictionary(g => g.Key, g => g.First().Resolution.LicenseText!);

        return new AttributionDocument(rows, licenseTexts, groupByLicense, embedLicenseText);
    }
}
