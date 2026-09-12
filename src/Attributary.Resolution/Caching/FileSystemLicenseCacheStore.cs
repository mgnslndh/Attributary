using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed class FileSystemLicenseCacheStore(string rootPath) : ILicenseCacheStore
{
    public string RootPath => rootPath;

    public CacheEntry? TryGet(CacheKey key)
    {
        var (contentPath, sidecarPath) = GetPaths(key);
        if (!File.Exists(contentPath) || !File.Exists(sidecarPath))
            return null;

        var sidecar = JsonSerializer.Deserialize<CacheSidecar>(File.ReadAllText(sidecarPath))!;
        var content = File.ReadAllText(contentPath);

        if (ComputeSha256(content) != sidecar.Sha256)
            return null;

        return new CacheEntry(content, sidecar.Sha256, sidecar.SourceUrl, sidecar.FetchedAtUtc);
    }

    public void Put(CacheKey key, CacheEntry entry)
    {
        var (contentPath, sidecarPath) = GetPaths(key);
        Directory.CreateDirectory(Path.GetDirectoryName(contentPath)!);
        File.WriteAllText(contentPath, entry.Content);
        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(new CacheSidecar(entry.Sha256, entry.SourceUrl, entry.FetchedAtUtc)));
    }

    public void Clear()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }

    public IReadOnlyList<CacheKey> List()
    {
        if (!Directory.Exists(rootPath)) return [];

        return Directory.GetFiles(rootPath, "*.sidecar.json", SearchOption.AllDirectories)
            .Select(sidecarPath =>
            {
                var strategy = Enum.Parse<ResolutionSourceStrategy>(Path.GetFileName(Path.GetDirectoryName(sidecarPath))!);
                var discriminator = Path.GetFileName(sidecarPath)[..^".sidecar.json".Length];
                return new CacheKey(strategy, discriminator);
            })
            .ToList();
    }

    private (string contentPath, string sidecarPath) GetPaths(CacheKey key)
    {
        var safeName = string.Join("_", key.Discriminator.Split(Path.GetInvalidFileNameChars()));
        var dir = Path.Combine(rootPath, key.Strategy.ToString());
        return (Path.Combine(dir, $"{safeName}.content"), Path.Combine(dir, $"{safeName}.sidecar.json"));
    }

    public static string ComputeSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private sealed record CacheSidecar(string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc);
}
