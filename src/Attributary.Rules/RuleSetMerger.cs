namespace Attributary.Rules;

public static class RuleSetMerger
{
    public static RuleSet Merge(RuleSet baseline, RuleSet? overrides)
    {
        if (overrides is null)
            return baseline;

        var overrideIds = overrides.Rules.Select(r => r.IdPattern).ToHashSet();
        var mergedRules = overrides.Rules
            .Concat(baseline.Rules.Where(r => !overrideIds.Contains(r.IdPattern)))
            .ToList();

        return new RuleSet(overrides.UnknownLicenseDefault, mergedRules);
    }
}
