using Attributary.Artifacts;
using Attributary.Diagnostics;
using Attributary.Resolution;
using Attributary.Rules;
using Attributary.Sbom;

namespace Attributary.Cli.Pipeline;

public sealed class GenerateOrchestrator(
    CycloneDxIngestor ingestor,
    ILicenseResolutionChain resolutionChain,
    IObligationPlanBuilder obligationPlanBuilder,
    IDiagnosticSink diagnostics)
{
    public async Task<GenerateOutput> RunAsync(
        string sbomPath,
        RuleSet ruleSet,
        bool groupByLicense,
        bool embedLicenseText,
        bool failFast,
        CancellationToken ct)
    {
        var components = ingestor.Ingest(sbomPath);
        var plans = new List<ObligationPlan>();

        foreach (var component in components)
        {
            var resolution = await resolutionChain.ResolveAsync(component, diagnostics, ct);
            var conditionResults = new Dictionary<string, bool>
            {
                ["upstream-notice-present"] = resolution.NoticeText is not null
            };
            var plan = obligationPlanBuilder.Build(resolution, ruleSet, conditionResults);
            plans.Add(plan);

            if (resolution.ResolvedLicenseIds.Count > 0)
            {
                var context = $"{component.Name} {component.Version}";
                ReportPolicyDiagnostic(plan, context);
                ReportUnresolvedObligations(plan, context);
            }

            if (failFast && diagnostics.HasErrors)
                break;
        }

        return new GenerateOutput(
            plans,
            LicenseTextsDocumentBuilder.Build(plans),
            NoticeDocumentBuilder.Build(plans),
            AttributionDocumentBuilder.Build(plans, groupByLicense, embedLicenseText),
            ComplianceReportDocumentBuilder.Build(plans));
    }

    private void ReportPolicyDiagnostic(ObligationPlan plan, string context)
    {
        var licenseLabel = string.Join(" AND ", plan.Resolution.ResolvedLicenseIds);
        if (plan.Policy == LicensePolicy.Deny)
            diagnostics.Report(RuleEngineDiagnostics.PolicyDenied, $"License '{licenseLabel}' is denied by policy.", context);
        else if (plan.Policy == LicensePolicy.Warn)
            diagnostics.Report(RuleEngineDiagnostics.PolicyWarn, $"License '{licenseLabel}' requires manual review.", context);
    }

    private void ReportUnresolvedObligations(ObligationPlan plan, string context)
    {
        foreach (var obligation in plan.Obligations)
        {
            if (obligation.Kind == ObligationKind.LicenseText)
            {
                // Checked per atom: a multi-license component can have some
                // license ids resolved and others not (spec §6a).
                foreach (var licenseId in plan.Resolution.ResolvedLicenseIds)
                {
                    if (!plan.Resolution.LicenseTextsByLicenseId.ContainsKey(licenseId))
                        diagnostics.Report(RuleEngineDiagnostics.RequiredObligationUnresolved,
                            $"Required obligation 'LicenseText' could not be resolved for license '{licenseId}'.", context);
                }
                continue;
            }

            var resolved = obligation.Kind switch
            {
                ObligationKind.Copyright => plan.Resolution.CopyrightText is not null,
                ObligationKind.NoticeText => plan.Resolution.NoticeText is not null,
                _ => true
            };

            if (!resolved)
            {
                var licenseLabel = string.Join(" AND ", plan.Resolution.ResolvedLicenseIds);
                diagnostics.Report(RuleEngineDiagnostics.RequiredObligationUnresolved,
                    $"Required obligation '{obligation.Kind}' could not be resolved for license '{licenseLabel}'.", context);
            }
        }
    }
}
