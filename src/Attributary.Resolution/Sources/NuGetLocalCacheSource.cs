using System.Xml.Linq;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class NuGetLocalCacheSource(string globalPackagesFolderPath) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.LocalPackageCache;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        if (component.Purl is null || !component.Purl.StartsWith("pkg:nuget/", StringComparison.Ordinal))
            return Task.FromResult(SourceResult.Unresolved);

        var packageId = ExtractPackageId(component.Purl);
        var packageDir = Path.Combine(globalPackagesFolderPath, packageId.ToLowerInvariant(), component.Version.ToLowerInvariant());
        var nuspecPath = Path.Combine(packageDir, $"{packageId.ToLowerInvariant()}.nuspec");

        if (!File.Exists(nuspecPath))
            return Task.FromResult(SourceResult.Unresolved);

        var doc = XDocument.Load(nuspecPath);
        var ns = doc.Root!.GetDefaultNamespace();
        var metadata = doc.Root!.Element(ns + "metadata")!;

        var copyright = metadata.Element(ns + "copyright")?.Value;
        var licenseElement = metadata.Element(ns + "license");
        string? licenseText = null;

        if (licenseElement is not null && (string?)licenseElement.Attribute("type") == "file")
        {
            var licenseFilePath = Path.Combine(packageDir, licenseElement.Value);
            if (File.Exists(licenseFilePath))
                licenseText = File.ReadAllText(licenseFilePath);
        }

        if (copyright is null && licenseText is null)
            return Task.FromResult(SourceResult.Unresolved);

        var provenance = new FieldProvenance(Strategy, nuspecPath, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, licenseText, copyright, null, provenance));
    }

    private static string ExtractPackageId(string purl) => purl["pkg:nuget/".Length..].Split('@')[0];
}
