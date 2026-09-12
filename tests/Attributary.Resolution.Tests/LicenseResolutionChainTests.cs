using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

file sealed class FakeSource(ResolutionSourceStrategy strategy, SourceResult result) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => strategy;
    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        => Task.FromResult(result);
}

public class LicenseResolutionChainTests
{
    private static SbomComponent BuildComponent(LicenseExpression license) =>
        new("Foo", "1.0.0", null, license, null, [], []);

    [Test]
    public async Task ResolveAsync_FirstSourceResolvesLicenseText_StopsAtFirstMatch()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SbomEmbedded, null, DateTimeOffset.UtcNow, false);
        var sources = new ILicenseSource[]
        {
            new FakeSource(ResolutionSourceStrategy.SbomEmbedded, new SourceResult(true, "MIT text", "Copyright X", null, provenance)),
            new FakeSource(ResolutionSourceStrategy.SpdxCanonical, new SourceResult(true, "different text", null, null, provenance))
        };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseText).IsEqualTo("MIT text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright X");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_NoSourceResolves_LeavesFieldsNullWithoutReportingADiagnostic()
    {
        var sources = new ILicenseSource[] { new FakeSource(ResolutionSourceStrategy.SbomEmbedded, SourceResult.Unresolved) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseText).IsNull();
        await Assert.That(resolution.CopyrightText).IsNull();
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_UnresolvedOrExpression_ReportsDiagnosticAndDoesNotCallSources()
    {
        var callCount = 0;
        var sources = new ILicenseSource[] { new CountingFakeSource(() => callCount++) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromExpression("(MIT OR Apache-2.0)")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseId).IsNull();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
    }

    private sealed class CountingFakeSource(Action onCalled) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            onCalled();
            return Task.FromResult(SourceResult.Unresolved);
        }
    }
}
