namespace Attributary.Resolution.Caching;

public interface ILicenseCacheStore
{
    string RootPath { get; }
    CacheEntry? TryGet(CacheKey key);
    void Put(CacheKey key, CacheEntry entry);
    void Clear();
    IReadOnlyList<CacheKey> List();
}
