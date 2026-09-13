using Attributary.Rules;

namespace Attributary.Artifacts;

public static class ComplianceReportDocumentBuilder
{
    public static ComplianceReportDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans.Select(p => new ComplianceReportEntry(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Resolution.Provenance?.LicenseTextProvenance?.Strategy,
                p.Obligations.Select(o => o.Kind).ToList()))
            .ToList();

        var flagged = plans
            .Where(p => p.Flags.Count > 0 || p.Policy != LicensePolicy.Allow)
            .Select(p => new ReviewFlagEntry(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Flags,
                p.Policy))
            .ToList();

        return new ComplianceReportDocument(entries, flagged);
    }
}
