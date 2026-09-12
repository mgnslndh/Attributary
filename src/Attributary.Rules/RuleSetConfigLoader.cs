namespace Attributary.Rules;

public static class RuleSetConfigLoader
{
    public static RuleSet LoadMerged(string? configPath, IDefaultRuleSetProvider defaultProvider, IRuleSetLoader loader)
    {
        var baseline = defaultProvider.Load();
        if (configPath is null || !File.Exists(configPath))
            return baseline;

        var overrideRuleSet = loader.Load(File.ReadAllText(configPath));
        return RuleSetMerger.Merge(baseline, overrideRuleSet);
    }
}
