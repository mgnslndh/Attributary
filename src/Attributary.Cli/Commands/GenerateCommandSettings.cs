using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class GenerateCommandSettings : CommandSettings
{
    [CommandOption("--sbom <PATH>")]
    public required string SbomPath { get; init; }

    [CommandOption("--out <DIR>")]
    public string OutDir { get; init; } = "./compliance";

    [CommandOption("--format <FORMATS>")]
    public string Format { get; init; } = "txt";

    [CommandOption("--group-by <MODE>")]
    public string GroupBy { get; init; } = "license";

    [CommandOption("--no-embed-license-text")]
    public bool NoEmbedLicenseText { get; init; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [CommandOption("--fail-fast")]
    public bool FailFast { get; init; }

    [CommandOption("--no-cache")]
    public bool NoCache { get; init; }

    [CommandOption("--cache-dir <PATH>")]
    public string? CacheDir { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }

    [CommandOption("--warnaserror")]
    public bool WarnAsErrorAll { get; init; }

    [CommandOption("--warnaserror-codes <CODES>")]
    public string? WarnAsErrorCodes { get; init; }

    [CommandOption("--warnaserror-exempt <CODES>")]
    public string? WarnAsErrorExempt { get; init; }

    [CommandOption("--nowarn <CODES>")]
    public string? NoWarnCodes { get; init; }

    [CommandOption("--severity <PAIRS>")]
    public string? Severity { get; init; }

    [CommandOption("--use-evidence")]
    public bool UseEvidence { get; init; }
}
