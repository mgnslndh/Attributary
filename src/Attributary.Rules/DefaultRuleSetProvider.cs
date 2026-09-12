using System.Reflection;

namespace Attributary.Rules;

public sealed class DefaultRuleSetProvider(IRuleSetLoader loader) : IDefaultRuleSetProvider
{
    public RuleSet Load() => loader.Load(LoadRawYaml());

    public string LoadRawYaml()
    {
        var assembly = typeof(DefaultRuleSetProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("DefaultRules.yaml"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
