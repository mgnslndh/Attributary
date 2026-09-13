namespace Attributary.Rules;

public sealed class RuleMatcher : IRuleMatcher
{
    public LicenseRule Match(string licenseId, RuleSet ruleSet)
    {
        var exact = ruleSet.Rules.FirstOrDefault(r => r.IdPattern == licenseId);
        if (exact is not null)
            return exact;

        var glob = ruleSet.Rules.FirstOrDefault(r => r.IdPattern.EndsWith('*')
            && licenseId.StartsWith(r.IdPattern[..^1], StringComparison.Ordinal));
        if (glob is not null)
            return glob;

        return ruleSet.UnknownLicenseDefault;
    }

    // The rule engine's universal entry point (ObligationPlanBuilder always calls
    // this, even for a single resolved id — behaviorally identical to Match for a
    // 1-element list). See spec §6a.
    public LicenseRule MatchExpression(IReadOnlyList<string> licenseIds, RuleSet ruleSet)
    {
        var matched = licenseIds.Select(id => Match(id, ruleSet)).ToList();

        var mostRestrictivePolicy = matched.Select(r => r.Policy).Max();
        var unionRequire = matched.SelectMany(r => r.Require).DistinctBy(o => (o.Kind, o.Condition)).ToList();
        var unionFlags = matched.SelectMany(r => r.Flags).Distinct().ToList();

        return new LicenseRule(string.Join(" AND ", licenseIds), mostRestrictivePolicy, unionRequire, unionFlags);
    }
}
