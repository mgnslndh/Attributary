namespace Attributary.Diagnostics;

public interface IDiagnosticSink
{
    IReadOnlyList<Diagnostic> Diagnostics { get; }
    bool HasErrors { get; }
    void Report(DiagnosticDescriptor descriptor, string message, string? context = null);
}
