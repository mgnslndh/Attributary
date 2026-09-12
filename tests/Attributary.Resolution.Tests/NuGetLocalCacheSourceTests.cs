using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

public class NuGetLocalCacheSourceTests
{
    private static string CreateFakePackage(string id, string version, string copyright, string? licenseFileContent)
    {
        var root = Path.Combine(Path.GetTempPath(), "attributary-tests", Guid.NewGuid().ToString());
        var packageDir = Path.Combine(root, id.ToLowerInvariant(), version.ToLowerInvariant());
        Directory.CreateDirectory(packageDir);

        var licenseElement = licenseFileContent is null
            ? ""
            : "<license type=\"file\">LICENSE.txt</license>";

        File.WriteAllText(Path.Combine(packageDir, $"{id.ToLowerInvariant()}.nuspec"), $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                <copyright>{copyright}</copyright>
                {licenseElement}
              </metadata>
            </package>
            """);

        if (licenseFileContent is not null)
            File.WriteAllText(Path.Combine(packageDir, "LICENSE.txt"), licenseFileContent);

        return root;
    }

    [Test]
    public async Task TryResolveAsync_NuspecWithCopyrightAndLicenseFile_ResolvesBoth()
    {
        var root = CreateFakePackage("Foo.Bar", "1.2.3", "Copyright (c) Foo Corp", "MIT License full text");
        var component = new SbomComponent("Foo.Bar", "1.2.3", "pkg:nuget/Foo.Bar@1.2.3", LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(root);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.CopyrightText).IsEqualTo("Copyright (c) Foo Corp");
        await Assert.That(result.LicenseText).IsEqualTo("MIT License full text");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.LocalPackageCache);
    }

    [Test]
    public async Task TryResolveAsync_PackageNotInCache_IsUnresolved()
    {
        var root = Path.Combine(Path.GetTempPath(), "attributary-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        var component = new SbomComponent("Missing.Package", "1.0.0", "pkg:nuget/Missing.Package@1.0.0", LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(root);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task TryResolveAsync_NonNuGetComponent_IsUnresolved()
    {
        var component = new SbomComponent("libcurl", "8.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(Path.GetTempPath());

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
