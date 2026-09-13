using Attributary.Cli.Pipeline;
using Attributary.Diagnostics;
using Attributary.Output;
using Spectre.Console;

namespace Attributary.Cli.Tests.Pipeline;

public class GenerateRunnerTests
{
    private static string WriteFixtureSbom(string licenseId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, $$"""
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Foo", "version": "1.0.0",
                  "copyright": "Copyright (c) Foo Inc.",
                  "licenses": [ { "license": { "id": "{{licenseId}}" } } ] }
              ]
            }
            """);
        return path;
    }

    [Test]
    public async Task RunAsync_MitComponent_WritesLicenseAttributionAndReportFiles()
    {
        var sbomPath = WriteFixtureSbom("MIT");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.Exists(Path.Combine(outDir, "LICENSES", "MIT.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outDir, "THIRD-PARTY-NOTICES.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outDir, "COMPLIANCE-REPORT.txt"))).IsTrue();
    }

    [Test]
    public async Task RunAsync_DenyPolicyLicense_ReturnsNonZeroExitCode()
    {
        var sbomPath = WriteFixtureSbom("JSON");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    private static string WriteEvidenceOnlyCopyrightFixtureSbom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Foo", "version": "1.0.0",
                  "licenses": [ { "license": { "id": "MIT" } } ],
                  "evidence": { "copyright": [ { "text": "Copyright (c) Evidence Co." } ] } }
              ]
            }
            """);
        return path;
    }

    [Test]
    public async Task RunAsync_EvidenceOnlyCopyright_WithoutUseEvidenceFlag_LeavesCopyrightObligationUnresolved()
    {
        var sbomPath = WriteEvidenceOnlyCopyrightFixtureSbom();
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null), UseEvidence: false);
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_EvidenceOnlyCopyright_WithUseEvidenceFlag_ResolvesCopyrightObligation()
    {
        var sbomPath = WriteEvidenceOnlyCopyrightFixtureSbom();
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null), UseEvidence: true);
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
    }

    [Test]
    public async Task RunAsync_DryRun_DoesNotCreateOutputDirectory()
    {
        var sbomPath = WriteFixtureSbom("MIT");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: true, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(Directory.Exists(outDir)).IsFalse();
    }
}
