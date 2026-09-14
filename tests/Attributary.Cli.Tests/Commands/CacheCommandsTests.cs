using Attributary.Cli.Commands;
using Attributary.Domain;
using Attributary.Resolution.Caching;
using Spectre.Console.Cli;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class CacheCommandsTests
{
    [Test]
    public async Task CachePathCommand_NoOverride_PrintsOsStandardCacheDirectory()
    {
        var console = new TestConsole();
        ICommand command = new CachePathCommand(console);

        var exitCode = await command.ExecuteAsync(null!, new CacheDirCommandSettings { CacheDir = null }, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Attributary");
    }

    [Test]
    public async Task CacheListCommand_ThenClearCommand_ListsThenEmpties()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"attributary-cache-{Guid.NewGuid()}");
        var store = new FileSystemLicenseCacheStore(cacheDir);
        store.Put(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT"),
            new CacheEntry("text", FileSystemLicenseCacheStore.ComputeSha256("text"), null, DateTimeOffset.UtcNow));

        var listConsole = new TestConsole();
        ICommand listCommand = new CacheListCommand(listConsole);
        await listCommand.ExecuteAsync(null!, new CacheDirCommandSettings { CacheDir = cacheDir }, CancellationToken.None);
        await Assert.That(listConsole.Output).Contains("MIT");

        var clearConsole = new TestConsole();
        ICommand clearCommand = new CacheClearCommand(clearConsole);
        await clearCommand.ExecuteAsync(null!, new CacheDirCommandSettings { CacheDir = cacheDir }, CancellationToken.None);
        await Assert.That(store.List()).IsEmpty();
    }
}
