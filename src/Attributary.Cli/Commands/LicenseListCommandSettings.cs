using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseListCommandSettings : CommandSettings
{
    [CommandOption("--sbom <PATH>")]
    public required string SbomPath { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }
}
