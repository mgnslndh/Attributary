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

        CacheSidecar? sidecar;
        string content;
        try
        {
            sidecar = JsonSerializer.Deserialize<CacheSidecar>(File.ReadAllText(sidecarPath));
            content = File.ReadAllText(contentPath);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (sidecar is null)
            return null;

        if (ComputeSha256(content) != sidecar.Sha256)
            return null;

        return new CacheEntry(content, sidecar.Sha256, sidecar.SourceUrl, sidecar.FetchedAtUtc);
    }

    public void Put(CacheKey key, CacheEntry entry)
    {
        var (contentPath, sidecarPath) = GetPaths(key);
        Directory.CreateDirectory(Path.GetDirectoryName(contentPath)!);
        File.WriteAllText(contentPath, entry.Content);
        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(new CacheSidecar(key.Discriminator, entry.Sha256, entry.SourceUrl, entry.FetchedAtUtc)));
    }

    public void Clear()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }

    public IReadOnlyList<CacheKey> List()
    {
        if (!Directory.Exists(rootPath)) return [];

        var keys = new List<CacheKey>();
        foreach (var sidecarPath in Directory.GetFiles(rootPath, "*.sidecar.json", SearchOption.AllDirectories))
        {
            CacheSidecar? sidecar;
            try
            {
                sidecar = JsonSerializer.Deserialize<CacheSidecar>(File.ReadAllText(sidecarPath));
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            if (sidecar is null)
                continue;

            var strategy = Enum.Parse<ResolutionSourceStrategy>(Path.GetFileName(Path.GetDirectoryName(sidecarPath))!);
            keys.Add(new CacheKey(strategy, sidecar.Discriminator));
        }

        return keys;
    }

    private (string contentPath, string sidecarPath) GetPaths(CacheKey key)
    {
        var hashedName = ComputeSha256(key.Discriminator);
        var dir = Path.Combine(rootPath, key.Strategy.ToString());
        return (Path.Combine(dir, $"{hashedName}.content"), Path.Combine(dir, $"{hashedName}.sidecar.json"));
    }

    public static string ComputeSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private sealed record CacheSidecar(string Discriminator, string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc);
}
