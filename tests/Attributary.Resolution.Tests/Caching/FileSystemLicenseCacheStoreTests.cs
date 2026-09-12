using Attributary.Domain;
using Attributary.Resolution.Caching;

namespace Attributary.Resolution.Tests.Caching;

public class FileSystemLicenseCacheStoreTests
{
    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "attributary-cache-tests", Guid.NewGuid().ToString());

    [Test]
    public async Task Put_ThenTryGet_RoundTripsContent()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        var entry = new CacheEntry("MIT text", FileSystemLicenseCacheStore.ComputeSha256("MIT text"), null, DateTimeOffset.UtcNow);

        store.Put(key, entry);
        var result = store.TryGet(key);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Content).IsEqualTo("MIT text");
    }

    [Test]
    public async Task TryGet_MissingKey_ReturnsNull()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var result = store.TryGet(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "Nonexistent"));
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryGet_TamperedContent_FailsIntegrityCheckAndReturnsNull()
    {
        var root = NewTempRoot();
        var store = new FileSystemLicenseCacheStore(root);
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        store.Put(key, new CacheEntry("original text", FileSystemLicenseCacheStore.ComputeSha256("original text"), null, DateTimeOffset.UtcNow));

        var contentPath = Directory.GetFiles(root, "*.content", SearchOption.AllDirectories).Single();
        File.WriteAllText(contentPath, "tampered text");

        var result = store.TryGet(key);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Clear_RemovesAllEntries()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        store.Put(key, new CacheEntry("text", FileSystemLicenseCacheStore.ComputeSha256("text"), null, DateTimeOffset.UtcNow));

        store.Clear();

        await Assert.That(store.TryGet(key)).IsNull();
        await Assert.That(store.List()).IsEmpty();
    }

    [Test]
    public async Task List_ReturnsAllStoredKeys()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        store.Put(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT"), new CacheEntry("a", FileSystemLicenseCacheStore.ComputeSha256("a"), null, DateTimeOffset.UtcNow));
        store.Put(new CacheKey(ResolutionSourceStrategy.VcsRepository, "pkg:nuget/Foo@1.0.0"), new CacheEntry("b", FileSystemLicenseCacheStore.ComputeSha256("b"), null, DateTimeOffset.UtcNow));

        var keys = store.List();

        await Assert.That(keys).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Put_DiscriminatorsThatSanitizeToSameString_DoNotCollide()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var keyA = new CacheKey(ResolutionSourceStrategy.VcsRepository, "pkg:nuget/Foo@1.0.0");
        var keyB = new CacheKey(ResolutionSourceStrategy.VcsRepository, "pkg_nuget_Foo@1.0.0");

        store.Put(keyA, new CacheEntry("content for A", FileSystemLicenseCacheStore.ComputeSha256("content for A"), null, DateTimeOffset.UtcNow));
        store.Put(keyB, new CacheEntry("content for B", FileSystemLicenseCacheStore.ComputeSha256("content for B"), null, DateTimeOffset.UtcNow));

        var resultA = store.TryGet(keyA);
        var resultB = store.TryGet(keyB);

        await Assert.That(resultA).IsNotNull();
        await Assert.That(resultA!.Content).IsEqualTo("content for A");
        await Assert.That(resultB).IsNotNull();
        await Assert.That(resultB!.Content).IsEqualTo("content for B");
    }

    [Test]
    public async Task List_PreservesOriginalUnsanitizedDiscriminator()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var key = new CacheKey(ResolutionSourceStrategy.VcsRepository, "pkg:nuget/Foo@1.0.0");
        store.Put(key, new CacheEntry("content", FileSystemLicenseCacheStore.ComputeSha256("content"), null, DateTimeOffset.UtcNow));

        var keys = store.List();

        await Assert.That(keys).Count().IsEqualTo(1);
        await Assert.That(keys[0].Discriminator).IsEqualTo("pkg:nuget/Foo@1.0.0");
    }
}
