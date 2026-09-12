namespace Attributary.Rules;

public interface IObligationPlanBuilder
{
    ObligationPlan Build(
        Attributary.Domain.LicenseResolution resolution,
        RuleSet ruleSet,
        IReadOnlyDictionary<string, bool> conditionResults);
}
