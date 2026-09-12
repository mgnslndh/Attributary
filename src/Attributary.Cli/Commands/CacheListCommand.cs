using Attributary.Resolution.Caching;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheListCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    protected override int Execute(CommandContext context, CacheDirCommandSettings settings, CancellationToken cancellationToken)
    {
        var store = new FileSystemLicenseCacheStore(CacheDirectoryResolver.Resolve(settings.CacheDir));
        foreach (var key in store.List())
            console.WriteLine($"{key.Strategy}: {key.Discriminator}");
        return 0;
    }
}
