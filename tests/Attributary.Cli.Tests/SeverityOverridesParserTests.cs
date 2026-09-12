using Attributary.Diagnostics;

namespace Attributary.Cli.Tests;

public class SeverityOverridesParserTests
{
    [Test]
    public async Task Parse_CommaListsAndPairs_ProducesExpectedSets()
    {
        var overrides = SeverityOverridesParser.Parse(
            warnAsErrorAll: true,
            warnAsErrorCodes: null,
            warnAsErrorExempt: "ATT2500",
            noWarnCodes: "ATT0002, ATT1001",
            severityPairs: "ATT3001=warning");

        await Assert.That(overrides.WarnAsErrorAll).IsTrue();
        await Assert.That(overrides.WarnAsErrorExemptCodes).Contains("ATT2500");
        await Assert.That(overrides.NoWarnCodes).Contains("ATT0002").And.Contains("ATT1001");
        await Assert.That(overrides.ExplicitSeverities["ATT3001"]).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Parse_AllNull_ReturnsEmptySets()
    {
        var overrides = SeverityOverridesParser.Parse(false, null, null, null, null);

        await Assert.That(overrides.WarnAsErrorAll).IsFalse();
        await Assert.That(overrides.WarnAsErrorCodes).IsEmpty();
        await Assert.That(overrides.ExplicitSeverities).IsEmpty();
    }
}
