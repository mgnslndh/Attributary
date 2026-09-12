namespace Attributary.Rules;

public interface IRuleMatcher
{
    LicenseRule Match(string licenseId, RuleSet ruleSet);
    LicenseRule MatchExpression(IReadOnlyList<string> licenseIds, RuleSet ruleSet);
}
