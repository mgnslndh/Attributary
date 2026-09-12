using System.Net;
using System.Text;
using Attributary.Domain;
using Attributary.Resolution.Sources;

namespace Attributary.Resolution.Tests;

file sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return Task.FromResult(response);
    }
}

public class GitHubVcsSourceTests
{
    [Test]
    public async Task TryResolveAsync_GitHubRepoWithLicense_DecodesBase64Content()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("MIT License full text"));
        var json = $$"""{ "content": "{{encoded}}", "html_url": "https://github.com/example/foo/blob/main/LICENSE" }""";
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, json));
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.Vcs, "https://github.com/example/foo")],
            Evidence: []);
        var source = new GitHubVcsSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.LicenseText).IsEqualTo("MIT License full text");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.VcsRepository);
    }

    [Test]
    public async Task TryResolveAsync_NoVcsExternalReference_IsUnresolved()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, null));
        var component = new SbomComponent("foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new GitHubVcsSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task TryResolveAsync_GitHubApiReturnsNotFound_IsUnresolved()
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.NotFound, null));
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.Vcs, "https://github.com/example/foo")],
            Evidence: []);
        var source = new GitHubVcsSource(httpClient);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
