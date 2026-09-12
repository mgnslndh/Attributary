namespace Attributary.Resolution.Caching;

public sealed record CacheEntry(string Content, string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc);
