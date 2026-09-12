using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseShowCommandSettings : CommandSettings
{
    [CommandArgument(0, "<LICENSE_ID>")]
    public required string LicenseId { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }
}
