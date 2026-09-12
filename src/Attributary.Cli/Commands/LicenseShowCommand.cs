using Attributary.Rules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseShowCommand(IAnsiConsole console) : Command<LicenseShowCommandSettings>
{
    protected override int Execute(CommandContext context, LicenseShowCommandSettings settings, CancellationToken cancellationToken)
    {
        var ruleSet = RuleSetConfigLoader.LoadMerged(settings.ConfigPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());
        var rule = new RuleMatcher().Match(settings.LicenseId, ruleSet);

        console.WriteLine($"License: {settings.LicenseId}");
        console.WriteLine($"Policy: {rule.Policy}");
        console.WriteLine("Requires:");
        foreach (var obligation in rule.Require)
            console.WriteLine($"  - {obligation.Kind}" + (obligation.Condition is null ? "" : $" (when: {obligation.Condition})"));
        console.WriteLine("Flags:");
        foreach (var flag in rule.Flags)
            console.WriteLine($"  - {flag}");

        return 0;
    }
}
