using Attributary.Rules;

namespace Attributary.Artifacts;

public static class LicenseTextsDocumentBuilder
{
    public static LicenseTextsDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.LicenseText))
            .SelectMany(p => p.Resolution.LicenseTextsByLicenseId)
            .GroupBy(kv => kv.Key)
            .Select(g => new LicenseTextEntry(g.Key, g.First().Value))
            .OrderBy(e => e.LicenseId, StringComparer.Ordinal)
            .ToList();

        return new LicenseTextsDocument(entries);
    }
}
