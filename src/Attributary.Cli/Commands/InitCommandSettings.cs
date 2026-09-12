using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class InitCommandSettings : CommandSettings
{
    [CommandOption("--out <PATH>")]
    public string OutPath { get; init; } = "attributary.config.yaml";
}
