namespace Attributary.Rules;

public sealed class ObligationPlanBuilder(IRuleMatcher matcher) : IObligationPlanBuilder
{
    public ObligationPlan Build(
        Attributary.Domain.LicenseResolution resolution,
        RuleSet ruleSet,
        IReadOnlyDictionary<string, bool> conditionResults)
    {
        var licenseId = resolution.ResolvedLicenseId ?? "UNKNOWN";
        var rule = matcher.Match(licenseId, ruleSet);

        var obligations = rule.Require
            .Where(o => o.Condition is null || conditionResults.GetValueOrDefault(o.Condition, false))
            .ToList();

        return new ObligationPlan(resolution, rule.Policy, obligations, rule.Flags);
    }
}
