namespace Attributary.Rules.Tests;

file sealed class FixedRuleSetProvider(RuleSet ruleSet) : IDefaultRuleSetProvider
{
    public RuleSet Load() => ruleSet;
    public string LoadRawYaml() => string.Empty;
}

file sealed class NeverCalledRuleSetLoader : IRuleSetLoader
{
    public RuleSet Load(string yamlContent) => throw new InvalidOperationException("Should not be called.");
}

public class RuleSetConfigLoaderTests
{
    private static RuleSet BuildBaseline() =>
        new(new LicenseRule("*", LicensePolicy.Deny, [], []), []);

    [Test]
    public async Task LoadMerged_NullConfigPath_ReturnsBaseline()
    {
        var baseline = BuildBaseline();
        var defaultProvider = new FixedRuleSetProvider(baseline);

        var result = RuleSetConfigLoader.LoadMerged(null, defaultProvider, new NeverCalledRuleSetLoader());

        await Assert.That(result).IsEqualTo(baseline);
    }

    [Test]
    public async Task LoadMerged_NonExistentConfigPath_ThrowsFileNotFoundException()
    {
        var defaultProvider = new FixedRuleSetProvider(BuildBaseline());
        var missingPath = Path.Combine(Path.GetTempPath(), $"attributary-missing-{Guid.NewGuid()}.yaml");

        await Assert.That(() => RuleSetConfigLoader.LoadMerged(missingPath, defaultProvider, new NeverCalledRuleSetLoader()))
            .Throws<FileNotFoundException>();
    }
}
