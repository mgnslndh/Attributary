using Attributary.Diagnostics;

namespace Attributary.Resolution;

public static class ResolutionDiagnostics
{
    public static readonly DiagnosticDescriptor OrExpressionUnresolved =
        new("ATT2001", DiagnosticSeverity.Error, "Unresolved SPDX license expression");
}
