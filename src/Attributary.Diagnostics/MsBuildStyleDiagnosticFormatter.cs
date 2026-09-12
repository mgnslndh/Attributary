namespace Attributary.Diagnostics;

public sealed class MsBuildStyleDiagnosticFormatter : IDiagnosticFormatter
{
    public string Format(Diagnostic diagnostic)
    {
        var severityLabel = diagnostic.EffectiveSeverity.ToString().ToLowerInvariant();
        var suffix = diagnostic.Context is null ? "" : $" [{diagnostic.Context}]";
        return $"{diagnostic.Descriptor.Code} {severityLabel}: {diagnostic.Message}{suffix}";
    }
}
