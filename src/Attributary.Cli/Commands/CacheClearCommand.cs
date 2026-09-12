using Attributary.Resolution.Caching;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheClearCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    protected override int Execute(CommandContext context, CacheDirCommandSettings settings, CancellationToken cancellationToken)
    {
        new FileSystemLicenseCacheStore(CacheDirectoryResolver.Resolve(settings.CacheDir)).Clear();
        console.WriteLine("Cache cleared.");
        return 0;
    }
}
