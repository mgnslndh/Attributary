using Attributary.Rules;
using Attributary.Sbom;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseListCommand(IAnsiConsole console) : Command<LicenseListCommandSettings>
{
    protected override int Execute(CommandContext context, LicenseListCommandSettings settings, CancellationToken cancellationToken)
    {
        var components = new CycloneDxIngestor().Ingest(settings.SbomPath);
        var bundled = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();
        var merged = RuleSetConfigLoader.LoadMerged(settings.ConfigPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());
        var matcher = new RuleMatcher();

        var table = new Table();
        table.AddColumn("Component");
        table.AddColumn("License");
        table.AddColumn("Policy");
        table.AddColumn("Custom rule");

        foreach (var component in components)
        {
            var licenseId = component.DeclaredLicense.SpdxId
                ?? component.DeclaredLicense.FreeTextName
                ?? component.DeclaredLicense.SpdxExpression
                ?? "UNKNOWN";
            var mergedRule = matcher.Match(licenseId, merged);
            var bundledRule = matcher.Match(licenseId, bundled);

            table.AddRow(
                $"{component.Name} {component.Version}",
                licenseId,
                mergedRule.Policy.ToString(),
                (!RulesAreEquivalent(mergedRule, bundledRule)).ToString());
        }

        console.Write(table);
        return 0;
    }

    private static bool RulesAreEquivalent(LicenseRule a, LicenseRule b) =>
        a.IdPattern == b.IdPattern
        && a.Policy == b.Policy
        && a.Require.Select(o => (o.Kind, o.Condition)).OrderBy(t => t.Kind).SequenceEqual(b.Require.Select(o => (o.Kind, o.Condition)).OrderBy(t => t.Kind))
        && a.Flags.OrderBy(f => f).SequenceEqual(b.Flags.OrderBy(f => f));
}
