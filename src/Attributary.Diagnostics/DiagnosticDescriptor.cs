namespace Attributary.Diagnostics;

public sealed record DiagnosticDescriptor(string Code, DiagnosticSeverity DefaultSeverity, string Title);
