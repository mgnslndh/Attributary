using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheDirCommandSettings : CommandSettings
{
    [CommandOption("--cache-dir <PATH>")]
    public string? CacheDir { get; init; }
}
