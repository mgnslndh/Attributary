namespace Attributary.Rules;

public interface IRuleSetLoader
{
    RuleSet Load(string yamlContent);
}
