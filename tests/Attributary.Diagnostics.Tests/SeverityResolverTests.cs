using Attributary.Diagnostics;

namespace Attributary.Diagnostics.Tests;

public class SeverityResolverTests
{
    private static readonly DiagnosticDescriptor WarningDescriptor =
        new("ATT2500", DiagnosticSeverity.Warning, "Cache integrity mismatch");

    private static readonly DiagnosticDescriptor ErrorDescriptor =
        new("ATT3001", DiagnosticSeverity.Error, "License policy deny");

    [Test]
    public async Task Resolve_NoOverrides_ReturnsDefaultSeverity()
    {
        var result = SeverityResolver.Resolve(WarningDescriptor, SeverityOverrides.None);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Resolve_WarnAsErrorAll_PromotesWarningToError()
    {
        var overrides = SeverityOverrides.None with { WarnAsErrorAll = true };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task Resolve_WarnAsErrorExempt_KeepsWarningEvenWithWarnAsErrorAll()
    {
        var overrides = SeverityOverrides.None with
        {
            WarnAsErrorAll = true,
            WarnAsErrorExemptCodes = new HashSet<string> { "ATT2500" }
        };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Resolve_NoWarn_SuppressesEvenWithWarnAsErrorAll()
    {
        var overrides = SeverityOverrides.None with
        {
            WarnAsErrorAll = true,
            NoWarnCodes = new HashSet<string> { "ATT2500" }
        };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task Resolve_ExplicitSeverity_WinsOverEverythingElse()
    {
        var overrides = SeverityOverrides.None with
        {
            NoWarnCodes = new HashSet<string> { "ATT3001" },
            ExplicitSeverities = new Dictionary<string, DiagnosticSeverity> { ["ATT3001"] = DiagnosticSeverity.Warning }
        };
        var result = SeverityResolver.Resolve(ErrorDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }
}
