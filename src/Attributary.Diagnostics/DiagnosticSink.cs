namespace Attributary.Diagnostics;

public sealed class DiagnosticSink(SeverityOverrides overrides) : IDiagnosticSink
{
    private readonly List<Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.EffectiveSeverity == DiagnosticSeverity.Error);

    public void Report(DiagnosticDescriptor descriptor, string message, string? context = null)
    {
        var severity = SeverityResolver.Resolve(descriptor, overrides);
        _diagnostics.Add(new Diagnostic(descriptor, severity, message, context));
    }
}
