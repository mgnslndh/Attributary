using Attributary.Cli.Pipeline;
using Attributary.Output;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class GenerateCommand(GenerateRunner runner) : AsyncCommand<GenerateCommandSettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GenerateCommandSettings settings, CancellationToken cancellationToken)
    {
        var formats = settings.Format
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(f => Enum.Parse<OutputFormat>(f, ignoreCase: true))
            .ToList();

        var options = new GenerateCliOptions(
            settings.SbomPath,
            settings.OutDir,
            formats,
            GroupByLicense: ParseGroupBy(settings.GroupBy),
            EmbedLicenseText: !settings.NoEmbedLicenseText,
            settings.DryRun,
            settings.FailFast,
            settings.NoCache,
            settings.CacheDir,
            settings.ConfigPath,
            SeverityOverridesParser.Parse(settings.WarnAsErrorAll, settings.WarnAsErrorCodes, settings.WarnAsErrorExempt, settings.NoWarnCodes, settings.Severity),
            UseEvidence: settings.UseEvidence);

        return await runner.RunAsync(options, cancellationToken);
    }

    private static bool ParseGroupBy(string groupBy) => groupBy.ToLowerInvariant() switch
    {
        "license" => true,
        "component" => false,
        _ => throw new ArgumentException($"Invalid --group-by value '{groupBy}'; expected 'license' or 'component'.")
    };
}
