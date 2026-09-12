namespace Attributary.Diagnostics;

public sealed record Diagnostic(
    DiagnosticDescriptor Descriptor,
    DiagnosticSeverity EffectiveSeverity,
    string Message,
    string? Context);
