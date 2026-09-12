using Attributary.Diagnostics;

namespace Attributary.Rules;

public static class RuleEngineDiagnostics
{
    public static readonly DiagnosticDescriptor PolicyDenied =
        new("ATT3001", DiagnosticSeverity.Error, "License policy denied");

    public static readonly DiagnosticDescriptor PolicyWarn =
        new("ATT3002", DiagnosticSeverity.Warning, "License policy requires review");

    public static readonly DiagnosticDescriptor RequiredObligationUnresolved =
        new("ATT3010", DiagnosticSeverity.Error, "Required data obligation unresolved");
}
