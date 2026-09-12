using Attributary.Cli.Commands;
using Attributary.Cli.Pipeline;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class GenerateCommandTests
{
    [Test]
    public async Task Execute_InvalidGroupByValue_ThrowsArgumentExceptionWithClearMessage()
    {
        var console = new TestConsole();
        ICommand command = new GenerateCommand(new GenerateRunner(console));
        var settings = new GenerateCommandSettings
        {
            SbomPath = "irrelevant.cdx.json",
            GroupBy = "licence"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            async () => await command.ExecuteAsync(null!, settings, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("Invalid --group-by value 'licence'");
    }
}
