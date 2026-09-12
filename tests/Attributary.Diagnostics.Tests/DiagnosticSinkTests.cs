namespace Attributary.Diagnostics.Tests;

public class DiagnosticSinkTests
{
    [Test]
    public async Task Report_AddsDiagnosticWithResolvedSeverity()
    {
        var sink = new DiagnosticSink(SeverityOverrides.None);
        var descriptor = new DiagnosticDescriptor("ATT3001", DiagnosticSeverity.Error, "License policy deny");

        sink.Report(descriptor, "Component X is denied", "X 1.0.0");

        await Assert.That(sink.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(sink.Diagnostics[0].EffectiveSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(sink.HasErrors).IsTrue();
    }

    [Test]
    public async Task HasErrors_FalseWhenOnlyWarnings()
    {
        var sink = new DiagnosticSink(SeverityOverrides.None);
        var descriptor = new DiagnosticDescriptor("ATT3002", DiagnosticSeverity.Warning, "License policy warn");

        sink.Report(descriptor, "Component Y needs review");

        await Assert.That(sink.HasErrors).IsFalse();
    }
}
