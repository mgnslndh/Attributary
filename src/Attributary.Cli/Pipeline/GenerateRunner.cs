using Attributary.Diagnostics;
using Attributary.Output;
using Attributary.Output.Html;
using Attributary.Output.Json;
using Attributary.Output.Markdown;
using Attributary.Output.Text;
using Attributary.Resolution;
using Attributary.Resolution.Caching;
using Attributary.Resolution.Sources;
using Attributary.Rules;
using Attributary.Sbom;
using Spectre.Console;

namespace Attributary.Cli.Pipeline;

public sealed class GenerateRunner(IAnsiConsole console)
{
    private static readonly IReadOnlyDictionary<OutputFormat, IAttributionWriter> AttributionWriters = new Dictionary<OutputFormat, IAttributionWriter>
    {
        [OutputFormat.Txt] = new TxtAttributionWriter(),
        [OutputFormat.Md] = new MdAttributionWriter(),
        [OutputFormat.Json] = new JsonAttributionWriter(),
        [OutputFormat.Html] = new HtmlAttributionWriter()
    };

    private static readonly IReadOnlyDictionary<OutputFormat, IComplianceReportWriter> ReportWriters = new Dictionary<OutputFormat, IComplianceReportWriter>
    {
        [OutputFormat.Txt] = new TxtComplianceReportWriter(),
        [OutputFormat.Md] = new MdComplianceReportWriter(),
        [OutputFormat.Json] = new JsonComplianceReportWriter(),
        [OutputFormat.Html] = new HtmlComplianceReportWriter()
    };

    public async Task<int> RunAsync(GenerateCliOptions options, CancellationToken ct)
    {
        var diagnostics = new DiagnosticSink(options.SeverityOverrides);
        var ruleSet = LoadRuleSet(options.ConfigPath);
        var chain = new LicenseResolutionChain(BuildSources(options));
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);

        var output = await orchestrator.RunAsync(options.SbomPath, ruleSet, options.GroupByLicense, options.EmbedLicenseText, options.FailFast, ct);

        if (!options.DryRun)
            WriteFiles(options, output);

        PrintDiagnostics(diagnostics);
        return diagnostics.HasErrors ? 1 : 0;
    }

    private static RuleSet LoadRuleSet(string? configPath) =>
        RuleSetConfigLoader.LoadMerged(configPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());

    private static IReadOnlyList<ILicenseSource> BuildSources(GenerateCliOptions options)
    {
        var cacheDir = CacheDirectoryResolver.Resolve(options.CacheDir);
        var store = new FileSystemLicenseCacheStore(cacheDir);

        ILicenseSource WithCache(ILicenseSource source) => options.NoCache ? source : new CachingLicenseSource(source, store);

        var globalPackagesFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        return
        [
            new SbomEmbeddedSource(),
            WithCache(new NuGetLocalCacheSource(globalPackagesFolder)),
            WithCache(new GitHubVcsSource(new HttpClient())),
            WithCache(new SpdxCanonicalSource())
        ];
    }

    private static void WriteFiles(GenerateCliOptions options, GenerateOutput output)
    {
        Directory.CreateDirectory(options.OutDir);

        var licensesDir = Path.Combine(options.OutDir, "LICENSES");
        Directory.CreateDirectory(licensesDir);
        foreach (var entry in output.LicenseTexts.Licenses)
            File.WriteAllText(Path.Combine(licensesDir, $"{entry.LicenseId}.txt"), entry.Text);

        if (output.Notice.Sections.Count > 0)
            File.WriteAllText(Path.Combine(options.OutDir, "NOTICE.txt"), new PlainTextNoticeWriter().Render(output.Notice));

        foreach (var format in options.Formats)
        {
            var extension = format.ToString().ToLowerInvariant();
            File.WriteAllText(Path.Combine(options.OutDir, $"THIRD-PARTY-NOTICES.{extension}"), AttributionWriters[format].Render(output.Attribution));
            File.WriteAllText(Path.Combine(options.OutDir, $"COMPLIANCE-REPORT.{extension}"), ReportWriters[format].Render(output.Report));
        }
    }

    private void PrintDiagnostics(IDiagnosticSink diagnostics)
    {
        var formatter = new MsBuildStyleDiagnosticFormatter();
        foreach (var diagnostic in diagnostics.Diagnostics)
            console.WriteLine(formatter.Format(diagnostic));

        var errorCount = diagnostics.Diagnostics.Count(d => d.EffectiveSeverity == DiagnosticSeverity.Error);
        var warningCount = diagnostics.Diagnostics.Count(d => d.EffectiveSeverity == DiagnosticSeverity.Warning);
        console.WriteLine($"{errorCount} error(s), {warningCount} warning(s)");
    }
}
