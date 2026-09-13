using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed partial class GitHubVcsSource(HttpClient httpClient) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.VcsRepository;

    public bool IsLicenseIdSpecific => false;

    public async Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        var vcsRef = component.ExternalReferences.FirstOrDefault(r => r.Type == ExternalReferenceType.Vcs);
        if (vcsRef is null || !TryParseGitHubRepo(vcsRef.Url, out var owner, out var repo))
            return SourceResult.Unresolved;

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}/license", ct);
        }
        catch (HttpRequestException)
        {
            return SourceResult.Unresolved;
        }

        if (!response.IsSuccessStatusCode)
            return SourceResult.Unresolved;

        var payload = await response.Content.ReadFromJsonAsync<GitHubLicenseResponse>(cancellationToken: ct);
        if (payload?.Content is null)
            return SourceResult.Unresolved;

        var decodedText = Encoding.UTF8.GetString(Convert.FromBase64String(payload.Content.Replace("\n", "")));
        var provenance = new FieldProvenance(Strategy, payload.HtmlUrl, DateTimeOffset.UtcNow, FromCache: false);
        return new SourceResult(true, decodedText, null, null, provenance);
    }

    private static bool TryParseGitHubRepo(string vcsUrl, out string owner, out string repo)
    {
        owner = ""; repo = "";
        var match = GitHubRepoPattern().Match(vcsUrl);
        if (!match.Success) return false;
        owner = match.Groups[1].Value;
        repo = match.Groups[2].Value;
        return true;
    }

    [GeneratedRegex(@"github\.com[/:]([^/]+)/([^/.]+?)(\.git)?/?$")]
    private static partial Regex GitHubRepoPattern();

    private sealed record GitHubLicenseResponse(
        string? Content,
        [property: JsonPropertyName("html_url")] string? HtmlUrl);
}
