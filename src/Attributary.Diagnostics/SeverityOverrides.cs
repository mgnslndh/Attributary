namespace Attributary.Diagnostics;

public sealed record SeverityOverrides(
    bool WarnAsErrorAll,
    IReadOnlySet<string> WarnAsErrorCodes,
    IReadOnlySet<string> WarnAsErrorExemptCodes,
    IReadOnlySet<string> NoWarnCodes,
    IReadOnlyDictionary<string, DiagnosticSeverity> ExplicitSeverities)
{
    public static readonly SeverityOverrides None = new(
        WarnAsErrorAll: false,
        WarnAsErrorCodes: new HashSet<string>(),
        WarnAsErrorExemptCodes: new HashSet<string>(),
        NoWarnCodes: new HashSet<string>(),
        ExplicitSeverities: new Dictionary<string, DiagnosticSeverity>());
}
