using Attributary.Rules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class InitCommand(IAnsiConsole console) : Command<InitCommandSettings>
{
    protected override int Execute(CommandContext context, InitCommandSettings settings, CancellationToken cancellationToken)
    {
        if (File.Exists(settings.OutPath))
        {
            console.WriteLine($"{settings.OutPath} already exists; not overwriting.");
            return 1;
        }

        var bundledYaml = new DefaultRuleSetProvider(new YamlRuleSetLoader()).LoadRawYaml();
        File.WriteAllText(settings.OutPath, bundledYaml);
        console.WriteLine($"Wrote starter config to {settings.OutPath}.");
        return 0;
    }
}
