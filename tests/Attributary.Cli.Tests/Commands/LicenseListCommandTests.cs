using Attributary.Cli.Commands;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class LicenseListCommandTests
{
    [Test]
    public async Task Execute_MitComponent_ListsAllowPolicy()
    {
        var sbomPath = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(sbomPath, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [ { "type": "library", "name": "Foo", "version": "1.0.0", "licenses": [ { "license": { "id": "MIT" } } ] } ]
            }
            """);
        var console = new TestConsole();
        ICommand command = new LicenseListCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new LicenseListCommandSettings { SbomPath = sbomPath, ConfigPath = null }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("MIT").And.Contains("Allow");
    }
}
