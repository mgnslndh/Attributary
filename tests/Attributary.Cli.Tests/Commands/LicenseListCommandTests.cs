using Attributary.Cli.Commands;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class LicenseListCommandTests
{
    private static string WriteMitSbom()
    {
        var sbomPath = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(sbomPath, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [ { "type": "library", "name": "Foo", "version": "1.0.0", "licenses": [ { "license": { "id": "MIT" } } ] } ]
            }
            """);
        return sbomPath;
    }

    [Test]
    public async Task Execute_MitComponent_ListsAllowPolicy()
    {
        var sbomPath = WriteMitSbom();
        var console = new TestConsole();
        ICommand command = new LicenseListCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new LicenseListCommandSettings { SbomPath = sbomPath, ConfigPath = null }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("MIT").And.Contains("Allow");
    }

    [Test]
    public async Task Execute_MitComponent_NoConfigOverride_CustomRuleColumnIsFalse()
    {
        var sbomPath = WriteMitSbom();
        var console = new TestConsole();
        ICommand command = new LicenseListCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new LicenseListCommandSettings { SbomPath = sbomPath, ConfigPath = null }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        // Two independent DefaultRuleSetProvider.Load() calls re-parse the bundled YAML fresh each
        // time, producing distinct List<T> object graphs for Require/Flags. The "Custom rule" column
        // must be based on a structural comparison, not record reference-equality on those lists, so
        // a license with no --config override must report "False" here.
        await Assert.That(console.Output).Contains("False");
        await Assert.That(console.Output).DoesNotContain("True");
    }

    [Test]
    public async Task Execute_MitComponent_ConfigOverridesPolicyToWarn_CustomRuleColumnIsTrue()
    {
        var sbomPath = WriteMitSbom();
        var configPath = Path.Combine(Path.GetTempPath(), $"attributary-config-{Guid.NewGuid()}.yaml");
        File.WriteAllText(configPath, """
            defaults:
              unknownLicense:
                policy: deny
                require: [copyright, license-text]

            rules:
              - id: MIT
                policy: warn
                require: [copyright, license-text]
            """);
        var console = new TestConsole();
        ICommand command = new LicenseListCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new LicenseListCommandSettings { SbomPath = sbomPath, ConfigPath = configPath }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Warn");
        await Assert.That(console.Output).Contains("True");
    }
}
