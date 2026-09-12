using Attributary.Cli.Commands;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class LicenseShowCommandTests
{
    [Test]
    public async Task Execute_Apache2_ShowsPolicyAndConditionalNoticeObligation()
    {
        var console = new TestConsole();
        ICommand command = new LicenseShowCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new LicenseShowCommandSettings { LicenseId = "Apache-2.0", ConfigPath = null }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Allow");
        await Assert.That(console.Output).Contains("NoticeText");
        await Assert.That(console.Output).Contains("upstream-notice-present");
    }
}
