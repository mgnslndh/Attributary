namespace Attributary.Diagnostics.Tests;

public class MsBuildStyleDiagnosticFormatterTests
{
    [Test]
    public async Task Format_WithContext_MatchesMsBuildStyle()
    {
        var descriptor = new DiagnosticDescriptor("ATT3001", DiagnosticSeverity.Error, "License policy deny");
        var diagnostic = new Diagnostic(descriptor, DiagnosticSeverity.Error, "License 'GPL-3.0-only' is denied", "Foo 1.2.3");
        var formatter = new MsBuildStyleDiagnosticFormatter();

        var result = formatter.Format(diagnostic);

        await Assert.That(result).IsEqualTo("ATT3001 error: License 'GPL-3.0-only' is denied [Foo 1.2.3]");
    }

    [Test]
    public async Task Format_WithoutContext_OmitsBrackets()
    {
        var descriptor = new DiagnosticDescriptor("ATT0002", DiagnosticSeverity.Error, "Unknown obligation");
        var diagnostic = new Diagnostic(descriptor, DiagnosticSeverity.Error, "Unknown obligation 'bogus'", null);
        var formatter = new MsBuildStyleDiagnosticFormatter();

        var result = formatter.Format(diagnostic);

        await Assert.That(result).IsEqualTo("ATT0002 error: Unknown obligation 'bogus'");
    }
}
