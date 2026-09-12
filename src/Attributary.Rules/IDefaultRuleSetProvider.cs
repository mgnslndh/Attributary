namespace Attributary.Rules;

public interface IDefaultRuleSetProvider
{
    RuleSet Load();
    string LoadRawYaml();
}
