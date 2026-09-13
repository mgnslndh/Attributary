using Attributary.Cli.Pipeline;
using Attributary.Diagnostics;
using Attributary.Resolution;
using Attributary.Resolution.Sources;
using Attributary.Rules;
using Attributary.Sbom;

namespace Attributary.Cli.Tests.Pipeline;

public class GenerateOrchestratorTests
{
    private static string WriteFixtureSbom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Foo", "version": "1.0.0",
                  "copyright": "Copyright (c) Foo Inc.",
                  "licenses": [ { "license": { "id": "MIT" } } ] }
              ]
            }
            """);
        return path;
    }

    [Test]
    public async Task RunAsync_MitComponentWithEmbeddedCopyright_ProducesCompleteDocumentsAndNoDiagnostics()
    {
        var sbomPath = WriteFixtureSbom();
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(sbomPath, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(output.LicenseTexts.Licenses).Count().IsEqualTo(1);
        await Assert.That(output.Attribution.Rows[0].Copyright).IsEqualTo("Copyright (c) Foo Inc.");
        await Assert.That(output.Report.FlaggedForReview).IsEmpty();
        await Assert.That(diagnostics.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task RunAsync_GplComponent_ReportsPolicyWarnDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Bar", "version": "2.0.0",
                  "copyright": "Copyright (c) Bar Inc.",
                  "licenses": [ { "license": { "id": "GPL-3.0-only" } } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        await orchestrator.RunAsync(path, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3002");
    }

    [Test]
    public async Task RunAsync_LicenseTextObligationUnresolved_ReportsRequiredObligationDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Baz", "version": "3.0.0",
                  "copyright": "Copyright (c) Baz Inc.",
                  "licenses": [ { "license": { "id": "MIT" } } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([]); // no sources at all -> license text never resolves
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        await orchestrator.RunAsync(path, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3010");
    }

    [Test]
    public async Task RunAsync_UnresolvableLicenseExpression_ReportsExactlyOneDiagnosticButStillIncludesComponentInReport()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Qux", "version": "4.0.0",
                  "copyright": "Copyright (c) Qux Inc.",
                  "licenses": [ { "expression": "(MIT OR Apache-2.0)" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(output.Report.Entries).Count().IsEqualTo(1);
        await Assert.That(output.Report.Entries[0].ComponentName).IsEqualTo("Qux");
    }

    [Test]
    public async Task RunAsync_FlatAndExpression_BothAtomsResolvable_AutoResolvesWithNoDiagnostics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Quux", "version": "5.0.0",
                  "copyright": "Copyright (c) Quux Inc.",
                  "licenses": [ { "expression": "MIT AND BSD-3-Clause" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics).IsEmpty();
        await Assert.That(output.Attribution.Rows[0].LicenseIds).Contains("MIT").And.Contains("BSD-3-Clause");
        await Assert.That(output.LicenseTexts.Licenses).Count().IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_FlatAndExpression_OneAtomUnresolvable_ReportsRequiredObligationUnresolvedForThatAtomOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Corge", "version": "6.0.0",
                  "copyright": "Copyright (c) Corge Inc.",
                  "licenses": [ { "expression": "MIT AND ISC" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3010");
        await Assert.That(diagnostics.Diagnostics.Single(d => d.Descriptor.Code == "ATT3010").Message).Contains("ISC");
        await Assert.That(output.LicenseTexts.Licenses.Select(e => e.LicenseId)).Contains("MIT");
        await Assert.That(output.LicenseTexts.Licenses.Select(e => e.LicenseId)).DoesNotContain("ISC");
    }

    [Test]
    public async Task RunAsync_NonFlatAndExpression_StillReportsAtt2001NotAnAutoResolve()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Grault", "version": "7.0.0",
                  "copyright": "Copyright (c) Grault Inc.",
                  "licenses": [ { "expression": "MIT AND (Apache-2.0 OR BSD-3-Clause)" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(output.Attribution.Rows[0].LicenseIds).Count().IsEqualTo(1);
        await Assert.That(output.Attribution.Rows[0].LicenseIds[0]).IsEqualTo("UNKNOWN");
    }
}
