using Attributary.Rules;

namespace Attributary.Artifacts;

public static class LicenseTextsDocumentBuilder
{
    public static LicenseTextsDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.LicenseText)
                && p.Resolution.ResolvedLicenseId is not null
                && p.Resolution.LicenseText is not null)
            .GroupBy(p => p.Resolution.ResolvedLicenseId!)
            .Select(g => new LicenseTextEntry(g.Key, g.First().Resolution.LicenseText!))
            .OrderBy(e => e.LicenseId, StringComparer.Ordinal)
            .ToList();

        return new LicenseTextsDocument(entries);
    }
}
