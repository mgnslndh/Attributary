using Attributary.Cli.Commands;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class InitCommandTests
{
    [Test]
    public async Task Execute_NoExistingFile_WritesBundledDefaultsConfig()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"attributary-config-{Guid.NewGuid()}.yaml");
        var console = new TestConsole();
        ICommand command = new InitCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new InitCommandSettings { OutPath = outPath }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.Exists(outPath)).IsTrue();
        await Assert.That(File.ReadAllText(outPath)).Contains("defaults:").And.Contains("MIT");
    }

    [Test]
    public async Task Execute_FileAlreadyExists_DoesNotOverwriteAndReturnsNonZero()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"attributary-config-{Guid.NewGuid()}.yaml");
        File.WriteAllText(outPath, "existing content");
        var console = new TestConsole();
        ICommand command = new InitCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new InitCommandSettings { OutPath = outPath }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(File.ReadAllText(outPath)).IsEqualTo("existing content");
    }
}
