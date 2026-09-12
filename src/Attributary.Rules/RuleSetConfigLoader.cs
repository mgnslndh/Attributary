namespace Attributary.Rules;

public static class RuleSetConfigLoader
{
    public static RuleSet LoadMerged(string? configPath, IDefaultRuleSetProvider defaultProvider, IRuleSetLoader loader)
    {
        var baseline = defaultProvider.Load();
        if (configPath is null)
            return baseline;

        if (!File.Exists(configPath))
            throw new FileNotFoundException($"Config file not found: {configPath}");

        var overrideRuleSet = loader.Load(File.ReadAllText(configPath));
        return RuleSetMerger.Merge(baseline, overrideRuleSet);
    }
}
