using Attributary.Domain;
using CycloneDX.Json;
using CycloneDX.Models;
using CdxExternalReference = CycloneDX.Models.ExternalReference;

namespace Attributary.Sbom;

public sealed class CycloneDxIngestor
{
    public IReadOnlyList<SbomComponent> Ingest(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var bom = Serializer.Deserialize(json);
        var components = bom.Components ?? [];

        return components.Select(MapComponent).ToList();
    }

    private static SbomComponent MapComponent(Component component)
    {
        var externalRefs = (component.ExternalReferences ?? [])
            .Select(r => new Attributary.Domain.ExternalReference(MapReferenceType(r.Type), r.Url))
            .ToList();

        var evidence = (component.Evidence?.Licenses ?? [])
            .Select(e => new LicenseEvidence(e.License?.Id, e.License?.Name, null))
            .ToList();

        return new SbomComponent(
            Name: component.Name,
            Version: component.Version,
            Purl: component.Purl,
            DeclaredLicense: MapDeclaredLicense(component.Licenses),
            RawCopyright: component.Copyright,
            ExternalReferences: externalRefs,
            Evidence: evidence);
    }

    private static LicenseExpression MapDeclaredLicense(List<LicenseChoice>? licenses)
    {
        if (licenses is null || licenses.Count == 0)
            return LicenseExpression.FromName("UNKNOWN");

        if (licenses.Count > 1)
        {
            var combined = string.Join(" AND ", licenses.Select(ResolveEntryIdentifier));
            return LicenseExpression.FromExpression(combined);
        }

        var first = licenses[0];
        if (first.Expression is not null)
            return LicenseExpression.FromExpression(first.Expression);

        if (first.License?.Id is not null)
            return LicenseExpression.FromId(first.License.Id);

        return LicenseExpression.FromName(first.License?.Name ?? "UNKNOWN");
    }

    private static string ResolveEntryIdentifier(LicenseChoice entry) =>
        entry.Expression ?? entry.License?.Id ?? entry.License?.Name ?? "UNKNOWN";

    private static ExternalReferenceType MapReferenceType(CdxExternalReference.ExternalReferenceType type) => type switch
    {
        CdxExternalReference.ExternalReferenceType.Vcs => ExternalReferenceType.Vcs,
        CdxExternalReference.ExternalReferenceType.License => ExternalReferenceType.License,
        CdxExternalReference.ExternalReferenceType.Website => ExternalReferenceType.Website,
        CdxExternalReference.ExternalReferenceType.Distribution => ExternalReferenceType.Distribution,
        _ => ExternalReferenceType.Other
    };
}
