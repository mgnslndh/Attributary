using Attributary.Diagnostics;

namespace Attributary.Cli;

public static class SeverityOverridesParser
{
    public static SeverityOverrides Parse(
        bool warnAsErrorAll, string? warnAsErrorCodes, string? warnAsErrorExempt, string? noWarnCodes, string? severityPairs) => new(
        WarnAsErrorAll: warnAsErrorAll,
        WarnAsErrorCodes: ParseCodeList(warnAsErrorCodes),
        WarnAsErrorExemptCodes: ParseCodeList(warnAsErrorExempt),
        NoWarnCodes: ParseCodeList(noWarnCodes),
        ExplicitSeverities: ParseSeverityPairs(severityPairs));

    private static IReadOnlySet<string> ParseCodeList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>()
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static IReadOnlyDictionary<string, DiagnosticSeverity> ParseSeverityPairs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Dictionary<string, DiagnosticSeverity>();

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Enum.Parse<DiagnosticSeverity>(parts[1], ignoreCase: true));
    }
}
