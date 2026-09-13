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
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Resolution.CopyrightText ?? ""))
            .OrderBy(r => r.ComponentName, StringComparer.Ordinal)
            .ToList();

        var licenseTexts = plans
            .SelectMany(p => p.Resolution.LicenseTextsByLicenseId)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        return new AttributionDocument(rows, licenseTexts, groupByLicense, embedLicenseText);
    }
}
