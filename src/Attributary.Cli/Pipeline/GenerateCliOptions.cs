using Attributary.Diagnostics;
using Attributary.Output;

namespace Attributary.Cli.Pipeline;

public sealed record GenerateCliOptions(
    string SbomPath,
    string OutDir,
    IReadOnlyList<OutputFormat> Formats,
    bool GroupByLicense,
    bool EmbedLicenseText,
    bool DryRun,
    bool FailFast,
    bool NoCache,
    string? CacheDir,
    string? ConfigPath,
    SeverityOverrides SeverityOverrides,
    bool UseEvidence = false);
