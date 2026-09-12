using Attributary.Domain;

namespace Attributary.Sbom.Tests;

public class CycloneDxIngestorTests
{
    [Test]
    public async Task Ingest_SimpleSpdxIdLicense_ReturnsSingleComponent()
    {
        var ingestor = new CycloneDxIngestor();

        var components = ingestor.Ingest(Path.Combine("fixtures", "simple-mit.cdx.json"));

        await Assert.That(components).HasCount().EqualTo(1);
        var component = components[0];
        await Assert.That(component.Name).IsEqualTo("Newtonsoft.Json");
        await Assert.That(component.Version).IsEqualTo("13.0.3");
        await Assert.That(component.Purl).IsEqualTo("pkg:nuget/Newtonsoft.Json@13.0.3");
        await Assert.That(component.RawCopyright).IsEqualTo("Copyright (c) 2007 James Newton-King");
        await Assert.That(component.DeclaredLicense.SpdxId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Ingest_ExpressionWithVcsAndEvidence_MapsAllFields()
    {
        var ingestor = new CycloneDxIngestor();

        var components = ingestor.Ingest(Path.Combine("fixtures", "expression-and-evidence.cdx.json"));

        var component = components[0];
        await Assert.That(component.DeclaredLicense.SpdxExpression).IsEqualTo("(MIT OR Apache-2.0)");
        await Assert.That(component.ExternalReferences).HasCount().EqualTo(1);
        await Assert.That(component.ExternalReferences[0].Type).IsEqualTo(ExternalReferenceType.Vcs);
        await Assert.That(component.ExternalReferences[0].Url).IsEqualTo("https://github.com/example/dual-licensed-lib");
        await Assert.That(component.Evidence).HasCount().EqualTo(1);
        await Assert.That(component.Evidence[0].SpdxId).IsEqualTo("MIT");
    }
}
