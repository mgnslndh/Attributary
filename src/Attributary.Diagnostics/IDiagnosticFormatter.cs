namespace Attributary.Diagnostics;

public interface IDiagnosticFormatter
{
    string Format(Diagnostic diagnostic);
}
