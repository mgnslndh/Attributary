using System.Net;
using System.Text;
using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

file sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string? body, string contentType = "text/plain") : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode);
        if (body is not null)
            response.Content = new StringContent(body, Encoding.UTF8, contentType);
        return Task.FromResult(response);
    }
}

public class SbomLicenseUrlSourceTests
{
    [Test]
    public async Task TryResolveAsync_LicenseExternalReferenceWithPlainTextResponse_ResolvesLicenseText()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "MIT License full text", "text/plain"));
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.License, "https://example.com/LICENSE")],
            Evidence: []);
        var source = new SbomLicenseUrlSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.LicenseText).IsEqualTo("MIT License full text");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.LicenseUrl);
        await Assert.That(result.Provenance!.SourceUrl).IsEqualTo("https://example.com/LICENSE");
    }

    [Test]
    public async Task TryResolveAsync_NoLicenseExternalReference_IsUnresolved()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, null));
        var component = new SbomComponent("foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new SbomLicenseUrlSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task TryResolveAsync_NonSuccessStatusCode_IsUnresolved()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.NotFound, null));
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.License, "https://example.com/LICENSE")],
            Evidence: []);
        var source = new SbomLicenseUrlSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task TryResolveAsync_HtmlResponse_IsUnresolved()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, "<html><body>Not a license</body></html>", "text/html"));
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.License, "https://example.com/LICENSE")],
            Evidence: []);
        var source = new SbomLicenseUrlSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
