using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CachePathCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    protected override int Execute(CommandContext context, CacheDirCommandSettings settings, CancellationToken cancellationToken)
    {
        console.WriteLine(CacheDirectoryResolver.Resolve(settings.CacheDir));
        return 0;
    }
}
