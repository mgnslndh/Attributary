using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

file sealed class FakeSource(ResolutionSourceStrategy strategy, SourceResult result, bool isLicenseIdSpecific = false) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => strategy;
    public bool IsLicenseIdSpecific => isLicenseIdSpecific;
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
            new FakeSource(ResolutionSourceStrategy.SpdxCanonical, new SourceResult(true, "different text", null, null, provenance), isLicenseIdSpecific: true)
        };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseTextsByLicenseId["MIT"]).IsEqualTo("MIT text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright X");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_NoSourceResolves_LeavesFieldsEmptyWithoutReportingADiagnostic()
    {
        var sources = new ILicenseSource[] { new FakeSource(ResolutionSourceStrategy.SbomEmbedded, SourceResult.Unresolved) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseTextsByLicenseId).IsEmpty();
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

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("enrich the SBOM with a single license id");
    }

    [Test]
    public async Task ResolveAsync_NonFlatAndExpression_StillReportsMultiLicenseFlavoredMessage()
    {
        // "MIT AND (Apache-2.0 OR BSD-3-Clause)" contains a paren -- not eligible
        // for automatic flat-AND resolution, so it keeps the existing diagnostic.
        var sources = new ILicenseSource[] { new CountingFakeSource(() => { }) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("MIT AND (Apache-2.0 OR BSD-3-Clause)")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("multi-license expression");
    }

    [Test]
    public async Task ResolveAsync_WithExpression_StillReportsMultiLicenseFlavoredMessageAndDoesNotCallSources()
    {
        // "GPL-2.0-only WITH Classpath-exception-2.0 AND MIT" contains a WITH
        // exception clause -- not eligible for automatic flat-AND resolution,
        // so it keeps the existing diagnostic and never calls a source.
        var callCount = 0;
        var sources = new ILicenseSource[] { new CountingFakeSource(() => callCount++) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("GPL-2.0-only WITH Classpath-exception-2.0 AND MIT")),
            sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("multi-license expression");
    }

    [Test]
    public async Task ResolveAsync_DuplicateAtomFlatAndExpression_CollapsesToOneAtomAndReportsDiagnostic()
    {
        // "MIT AND MIT" dedupes to a single atom, which is no longer eligible
        // for flat-AND auto-resolution -- it should fall back to the existing
        // ATT2001-style diagnostic rather than silently resolving to
        // ["MIT", "MIT"].
        var callCount = 0;
        var sources = new ILicenseSource[] { new CountingFakeSource(() => callCount++) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("MIT AND MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("multi-license expression");
    }

    [Test]
    public async Task ResolveAsync_FlatAndExpression_AutoResolvesEachAtomViaIdSpecificSourceOnly()
    {
        var canonicalProvenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var embeddedProvenance = new FieldProvenance(ResolutionSourceStrategy.SbomEmbedded, null, DateTimeOffset.UtcNow, false);
        var texts = new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" };

        var canonicalSource = new PerAtomFakeSource(texts, canonicalProvenance);
        var packageLevelSource = new FakeSource(
            ResolutionSourceStrategy.SbomEmbedded,
            new SourceResult(true, null, "Copyright Foo Inc.", null, embeddedProvenance));

        var sources = new ILicenseSource[] { packageLevelSource, canonicalSource };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("MIT AND Apache-2.0")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).Contains("MIT").And.Contains("Apache-2.0");
        await Assert.That(resolution.LicenseTextsByLicenseId["MIT"]).IsEqualTo("MIT text");
        await Assert.That(resolution.LicenseTextsByLicenseId["Apache-2.0"]).IsEqualTo("Apache text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright Foo Inc.");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_FlatAndExpression_PackageLevelSourceNeverAskedForLicenseText()
    {
        // A package-level source (IsLicenseIdSpecific: false) must never be asked
        // for license text in the multi-atom loop -- it would hand back the same
        // blob for every atom, misattributing it.
        var callCountForLicenseText = 0;
        var packageLevelSource = new CallCountingLicenseTextSource(() => callCountForLicenseText++);
        var canonicalSource = new PerAtomFakeSource(
            new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false));

        var chain = new LicenseResolutionChain([packageLevelSource, canonicalSource]);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        await chain.ResolveAsync(BuildComponent(LicenseExpression.FromExpression("MIT AND Apache-2.0")), sink, CancellationToken.None);

        await Assert.That(callCountForLicenseText).IsEqualTo(0);
    }

    private sealed class CountingFakeSource(Action onCalled) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;
        public bool IsLicenseIdSpecific => false;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            onCalled();
            return Task.FromResult(SourceResult.Unresolved);
        }
    }

    // Resolves license text per the exact atom id it's asked about, from a fixed
    // lookup table -- stands in for SpdxCanonicalSource's real per-id behavior.
    private sealed class PerAtomFakeSource(IReadOnlyDictionary<string, string> textsById, FieldProvenance provenance) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SpdxCanonical;
        public bool IsLicenseIdSpecific => true;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            if (licenseId is not null && textsById.TryGetValue(licenseId, out var text))
                return Task.FromResult(new SourceResult(true, text, null, null, provenance));
            return Task.FromResult(SourceResult.Unresolved);
        }
    }

    // A package-level (IsLicenseIdSpecific: false) source that would resolve
    // license text if ever asked -- used to prove the multi-atom loop never asks it.
    private sealed class CallCountingLicenseTextSource(Action onCalledForLicenseText) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.VcsRepository;
        public bool IsLicenseIdSpecific => false;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            // The copyright/notice loop legitimately calls every source (including
            // this one) with licenseId: null -- that is not "asking for license
            // text." Only a per-atom call (non-null licenseId) is what this test
            // guards against.
            if (licenseId is not null)
                onCalledForLicenseText();
            var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, false);
            return Task.FromResult(new SourceResult(true, "would-be-wrong-shared-text", null, null, provenance));
        }
    }
}
