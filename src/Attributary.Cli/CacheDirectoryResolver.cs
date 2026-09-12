namespace Attributary.Cli;

public static class CacheDirectoryResolver
{
    public static string Resolve(string? cacheDir) => cacheDir ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Attributary", "cache");
}
