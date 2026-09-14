# Dogfooding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Attributary run itself against its own real dependency tree as part of every build — generate a CycloneDX SBOM for `Attributary.Cli`'s shipped dependencies, run `attributary generate` against it, fail the build if the tool can't cleanly process its own output, embed the result in the `.nupkg`, and upload it as a browsable CI artifact. Nothing generated is committed to the repo.

**Architecture:** Four sequential tasks. Task 1 fixes a real credential-leak-risk bug (a GitHub token, once added, must never reach `SbomLicenseUrlSource`'s arbitrary third-party URL fetches) and is a genuine prerequisite for reliable dogfooding in CI. Task 2 adds the SBOM/compliance generation itself as a new, isolated Cake task. Task 3 wires that task into the existing `Pack` dependency chain and embeds the result in the package. Task 4 extends CI to surface the result as a browsable artifact.

**Tech Stack:** `CycloneDX` dotnet tool (6.2.0), the already-built `Attributary.Cli.dll`, Cake.Frosting (existing).

**Spec:** [`docs/superpowers/specs/2026-09-14-dogfooding-design.md`](../specs/2026-09-14-dogfooding-design.md)

## Global Constraints

- No SBOM or compliance artifact is ever committed to git. `artifacts/` is already in `.gitignore` — no new ignore rules needed.
- The GitHub token fix in Task 1 must apply the `Authorization` header only to `GitHubVcsSource`'s own per-request `HttpRequestMessage` — never to the shared `HttpClient`'s default headers, since that same client is passed to `SbomLicenseUrlSource`, which fetches arbitrary third-party URLs from SBOM data.
- `Dogfood` fails hard (`CakeException`) if either the SBOM generation or `attributary generate` itself exits non-zero. There is no drift check, no severity split, no Dependabot special-casing anywhere in this plan.
- SBOM scope is `Attributary.Cli.csproj` recursively (`-rs`) — its actual shipped dependency tree — not the whole solution.
- `CycloneDX` tool version: `6.2.0` (confirmed current via `dotnet tool list`).
- Output paths: `artifacts/sbom/attributary.cdx.json`, `artifacts/compliance/` (LICENSES/, NOTICE.txt if triggered, THIRD-PARTY-NOTICES.md, COMPLIANCE-REPORT.md — `--format md`).
- `Pack` depends on `Dogfood` (which still depends on `Build`), not on `Build` directly.
- All commands in this plan assume the current working directory is the repo root.

---

### Task 1: GitHub API authentication fix

**Files:**
- Modify: `src/Attributary.Resolution/Sources/GitHubVcsSource.cs`
- Modify: `src/Attributary.Cli/Pipeline/GenerateRunner.cs`
- Modify: `tests/Attributary.Resolution.Tests/GitHubVcsSourceTests.cs`

**Interfaces:**
- Produces: `GitHubVcsSource`'s constructor gains an optional `string? token = null` second parameter. Existing call sites that only pass `httpClient` keep compiling unchanged.

- [ ] **Step 1: Write failing tests for the new behavior**

In `tests/Attributary.Resolution.Tests/GitHubVcsSourceTests.cs`, first extend the existing `FakeHttpMessageHandler` to capture the last request it received, so tests can assert on its headers:

Change:

```csharp
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
```

to:

```csharp
file sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string? jsonBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(statusCode);
        if (jsonBody is not null)
            response.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return Task.FromResult(response);
    }
}
```

Then add two new test methods at the end of the `GitHubVcsSourceTests` class, right before its closing `}`:

```csharp
    [Test]
    public async Task TryResolveAsync_WithToken_SetsBearerAuthorizationHeader()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes("MIT License full text"));
        var json = $$"""{ "content": "{{encoded}}", "html_url": "https://github.com/example/foo/blob/main/LICENSE" }""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);
        var httpClient = new HttpClient(handler);
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.Vcs, "https://github.com/example/foo")],
            Evidence: []);
        var source = new GitHubVcsSource(httpClient, "test-token-123");

        await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Headers.Authorization).IsNotNull();
        await Assert.That(handler.LastRequest.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(handler.LastRequest.Headers.Authorization!.Parameter).IsEqualTo("test-token-123");
    }

    [Test]
    public async Task TryResolveAsync_WithoutToken_NoAuthorizationHeaderSet()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound, null);
        var httpClient = new HttpClient(handler);
        var component = new SbomComponent(
            "foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null,
            ExternalReferences: [new ExternalReference(ExternalReferenceType.Vcs, "https://github.com/example/foo")],
            Evidence: []);
        var source = new GitHubVcsSource(httpClient);

        await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(handler.LastRequest).IsNotNull();
        await Assert.That(handler.LastRequest!.Headers.Authorization).IsNull();
    }
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests -- --treenode-filter "/*/*/GitHubVcsSourceTests/*"`
Expected: FAIL — `GitHubVcsSource` has no constructor overload accepting a second `string?` argument yet, so this won't even compile. That compile failure is the expected "RED" state.

- [ ] **Step 3: Implement the constructor change and per-request header**

In `src/Attributary.Resolution/Sources/GitHubVcsSource.cs`, change:

```csharp
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
```

to:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed partial class GitHubVcsSource(HttpClient httpClient, string? token = null) : ILicenseSource
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
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/license");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            response = await httpClient.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            return SourceResult.Unresolved;
        }
```

The rest of the file (`TryParseGitHubRepo`, the regex, `GitHubLicenseResponse`) is unchanged.

- [ ] **Step 4: Run the tests again to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests -- --treenode-filter "/*/*/GitHubVcsSourceTests/*"`
Expected: PASS — all 5 tests (the 3 pre-existing plus the 2 new ones).

- [ ] **Step 5: Wire the token through from the environment**

In `src/Attributary.Cli/Pipeline/GenerateRunner.cs`, change:

```csharp
        var httpClient = new HttpClient();

        List<ILicenseSource> sources =
        [
            new SbomEmbeddedSource(),
            WithCache(new NuGetLocalCacheSource(globalPackagesFolder)),
            WithCache(new SbomLicenseUrlSource(httpClient)),
            WithCache(new GitHubVcsSource(httpClient))
        ];
```

to:

```csharp
        var httpClient = new HttpClient();
        var gitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

        List<ILicenseSource> sources =
        [
            new SbomEmbeddedSource(),
            WithCache(new NuGetLocalCacheSource(globalPackagesFolder)),
            WithCache(new SbomLicenseUrlSource(httpClient)),
            WithCache(new GitHubVcsSource(httpClient, gitHubToken))
        ];
```

Note that `SbomLicenseUrlSource(httpClient)` on the line above is deliberately left untouched — it must never receive the token, which is exactly why the token is threaded through as a `GitHubVcsSource`-only constructor argument rather than set on the shared `httpClient`.

- [ ] **Step 6: Run the full Resolution and Cli test suites**

Run: `dotnet test tests/Attributary.Resolution.Tests tests/Attributary.Cli.Tests`
Expected: all tests pass, 0 failures.

- [ ] **Step 7: Commit**

```bash
git add src/Attributary.Resolution/Sources/GitHubVcsSource.cs src/Attributary.Cli/Pipeline/GenerateRunner.cs tests/Attributary.Resolution.Tests/GitHubVcsSourceTests.cs
git commit -m "fix(resolution): authenticate GitHub API calls without leaking the token to other sources"
```

---

### Task 2: SBOM + compliance generation (`Dogfood` Cake task)

**Files:**
- Modify: `.config/dotnet-tools.json`
- Create: `build/Attributary.Build/Tasks/DogfoodTask.cs`

**Interfaces:**
- Consumes: the already-built `src/Attributary.Cli/bin/{Configuration}/net10.0/Attributary.Cli.dll` (from `BuildTask`, which this task depends on).
- Produces: `artifacts/sbom/attributary.cdx.json`, `artifacts/compliance/**` on disk when the task succeeds. Task 3 depends on both existing.

- [ ] **Step 1: Add the CycloneDX tool to the local tool manifest**

Run: `dotnet tool install --local CycloneDX --version 6.2.0`

Expected: succeeds, and `.config/dotnet-tools.json` gains a new entry for the tool (let the command generate this entry rather than hand-editing the file, to guarantee the exact key/command name match what's actually installable — verify afterward with `cat .config/dotnet-tools.json`, which should now show three tools: `dotnet-reportgenerator-globaltool`, `minver-cli`, and the new CycloneDX entry, each with `"version": "6.2.0"` for the new one).

- [ ] **Step 2: Create `build/Attributary.Build/Tasks/DogfoodTask.cs`**

```csharp
using Cake.Common;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Dogfood")]
[IsDependentOn(typeof(BuildTask))]
public sealed class DogfoodTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        const string sbomDir = "artifacts/sbom";
        const string sbomFileName = "attributary.cdx.json";
        const string complianceDir = "artifacts/compliance";

        context.EnsureDirectoryExists(sbomDir);

        var sbomExitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("tool").Append("run").Append("CycloneDX")
                .Append("--")
                .Append("src/Attributary.Cli/Attributary.Cli.csproj")
                .Append("-o").Append(sbomDir)
                .Append("-fn").Append(sbomFileName)
                .Append("-rs")
                .Append("-F").Append("Json")
                .Append("-ed")
                .Append("-c").Append(context.Configuration)
                .Append("-ns"),
        });

        if (sbomExitCode != 0)
        {
            throw new CakeException($"CycloneDX SBOM generation failed with exit code {sbomExitCode}.");
        }

        var sbomPath = $"{sbomDir}/{sbomFileName}";
        var cliDll = $"src/Attributary.Cli/bin/{context.Configuration}/net10.0/Attributary.Cli.dll";

        var generateExitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .AppendQuoted(cliDll)
                .Append("generate")
                .Append("--sbom").AppendQuoted(sbomPath)
                .Append("--out").AppendQuoted(complianceDir)
                .Append("--format").Append("md"),
        });

        if (generateExitCode != 0)
        {
            throw new CakeException(
                $"'attributary generate' failed against the project's own SBOM (exit code {generateExitCode}). " +
                "This means Attributary cannot cleanly process its own dependency tree -- see the diagnostic output above.");
        }

        context.Information($"Dogfood: generated SBOM at {sbomPath} and compliance bundle at {complianceDir}.");
    }
}
```

If any of these Cake API calls don't compile exactly as written against the installed `Cake.Frosting`/`Cake.Common` versions, treat it as normal debugging (check the compiler error, find the real signature, adjust) rather than a blocker — this is the same situation Task 5 of the release-foundation plan encountered and resolved the same way.

- [ ] **Step 3: Compile the build project**

Run: `dotnet build build/Attributary.Build`
Expected: succeeds, 0 errors.

- [ ] **Step 4: Run the new task directly and verify it against this repo's real dependency tree**

Run: `./build.ps1 Dogfood` (Windows) or `./build.sh Dogfood` (other platforms) — this is the first real end-to-end proof this design works, not just that it compiles.

Expected: succeeds (exit 0). Confirm `artifacts/sbom/attributary.cdx.json` exists and is valid JSON. Confirm `artifacts/compliance/` contains at least `LICENSES/` (with `.txt` files), `THIRD-PARTY-NOTICES.md`, and `COMPLIANCE-REPORT.md`. Read `COMPLIANCE-REPORT.md` and confirm it lists real dependencies (Spectre.Console, YamlDotNet, CycloneDX.Core, etc.) with `allow`-policy licenses and no denials — this is the point in the plan where the spec's carried-forward risk ("no policy denial is expected, but not yet verified end-to-end") actually gets resolved. If a denial or unexpected diagnostic DOES appear, stop and report it rather than proceeding — that's a real finding, not something to work around silently.

- [ ] **Step 5: Commit**

```bash
git add .config/dotnet-tools.json build/Attributary.Build/Tasks/DogfoodTask.cs
git commit -m "build: add Dogfood task generating Attributary's own SBOM and compliance bundle"
```

(Do not `git add artifacts/` — it's gitignored, and Step 4's output must not be committed, per this plan's Global Constraints.)

---

### Task 3: `Pack` depends on `Dogfood`, bundle embedded in the `.nupkg`

**Files:**
- Modify: `build/Attributary.Build/Tasks/PackTask.cs`
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj`

**Interfaces:**
- Consumes: `artifacts/sbom/attributary.cdx.json` and `artifacts/compliance/**` from Task 2's `DogfoodTask`.
- Produces: a `.nupkg` containing `sbom/attributary.cdx.json` and `compliance/**` alongside the existing embedded `README.md`.

- [ ] **Step 1: Change `PackTask`'s dependency from `Build` to `Dogfood`**

In `build/Attributary.Build/Tasks/PackTask.cs`, change:

```csharp
[TaskName("Pack")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackTask : FrostingTask<BuildContext>
```

to:

```csharp
[TaskName("Pack")]
[IsDependentOn(typeof(DogfoodTask))]
public sealed class PackTask : FrostingTask<BuildContext>
```

The rest of the file (the `Run` method's body) is unchanged. `DogfoodTask` already depends on `BuildTask`, so the effective chain (`Restore` → `Build` → `Dogfood` → `Pack`) is unchanged except for the new step in between.

- [ ] **Step 2: Add the pack-time items to `Attributary.Cli.csproj`**

Change:

```xml
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

to:

```xml
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
    <None Include="..\..\artifacts\sbom\attributary.cdx.json"
          Pack="true" PackagePath="sbom\"
          Condition="Exists('..\..\artifacts\sbom\attributary.cdx.json')" />
    <None Include="..\..\artifacts\compliance\**\*"
          Pack="true" PackagePath="compliance\"
          Condition="Exists('..\..\artifacts\compliance')" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Run the full pipeline and confirm the bundle lands in the package**

Run: `./build.ps1 Pack` (Windows) or `./build.sh Pack` (other platforms).
Expected: succeeds. This now runs `Restore` → `Build` → `Dogfood` → `Pack` in one go.

Confirm the bundle is actually in the package: copy `artifacts/Attributary.<version>.nupkg` to a `.zip` extension in a scratch location and list its contents (e.g. `Copy-Item artifacts/Attributary.*.nupkg scratch.zip; Expand-Archive scratch.zip -DestinationPath scratch-extracted` on Windows, or `unzip -l artifacts/Attributary.*.nupkg` on Linux/macOS).
Expected: the listing includes `sbom/attributary.cdx.json`, `compliance/LICENSES/...`, `compliance/THIRD-PARTY-NOTICES.md`, `compliance/COMPLIANCE-REPORT.md`, alongside the pre-existing `README.md`.

- [ ] **Step 4: Verify the `Condition="Exists(...)"` guard actually guards**

Run: `Remove-Item -Recurse -Force artifacts` (PowerShell) or `rm -rf artifacts` (bash) to simulate a from-scratch state, then run `dotnet pack src/Attributary.Cli/Attributary.Cli.csproj -c Release -o scratch-pack-test` directly (bypassing Cake, simulating someone packing manually without running `Dogfood` first).
Expected: succeeds (does not fail due to missing files), producing a package that does NOT contain `sbom/` or `compliance/` — confirming the guard works both ways (present → included, absent → silently skipped, never a hard error). Clean up `scratch-pack-test/` afterward — it's not meant to be committed or kept.

- [ ] **Step 5: Commit**

```bash
git add build/Attributary.Build/Tasks/PackTask.cs src/Attributary.Cli/Attributary.Cli.csproj
git commit -m "build: pack the CLI's own SBOM and compliance bundle into the nupkg"
```

---

### Task 4: CI artifact upload

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `artifacts/sbom/**` and `artifacts/compliance/**`, produced transitively by the existing `Pack` step (Task 3) once this plan lands.

- [ ] **Step 1: Pass `GITHUB_TOKEN` to the `Pack` step**

Fetch the current file content first (`gh api repos/mgnslndh/Attributary/contents/.github/workflows/ci.yml --jq '.content' | base64 -d`, or read the local working copy if it's in sync with `main`) since this file has been edited outside this plan's own worktree during earlier CI-polish work — do not assume the version already open in an editor is current.

Change the `Pack` step from:

```yaml
      - name: Pack
        if: runner.os == 'Linux'
        run: ./build.sh Pack
```

to:

```yaml
      - name: Pack
        if: runner.os == 'Linux'
        run: ./build.sh Pack
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

`secrets.GITHUB_TOKEN` is the token GitHub Actions provides automatically for every run — no new secret needs to be created. It flows from this step's environment down through `./build.sh` → Cake → `DogfoodTask`'s `StartProcess` calls → `Attributary.Cli.dll` → `GenerateRunner.BuildSources`'s `Environment.GetEnvironmentVariable("GITHUB_TOKEN")` read (Task 1), without any other plumbing needed — child processes inherit their parent's environment by default.

- [ ] **Step 2: Add the compliance-bundle upload step**

Immediately after the existing `Upload package artifact` step, add:

```yaml
      - name: Upload compliance bundle
        if: runner.os == 'Linux'
        uses: actions/upload-artifact@v7
        with:
          name: compliance-bundle
          path: |
            artifacts/sbom/**
            artifacts/compliance/**
          if-no-files-found: error
```

- [ ] **Step 3: Validate YAML syntax**

Run: `python -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); print('OK')"` (install pyyaml first if needed: `python -m pip install --quiet pyyaml`).
Expected: prints `OK`.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run Dogfood via Pack's dependency chain and upload the compliance bundle"
```

- [ ] **Step 5: Push and verify on GitHub (controller-executed, same reasoning as the CI-pipeline plan's branch-protection task)**

Pushing to a shared remote always needs the user's explicit, in-the-moment approval — ask before running `git push`. Once approved and pushed, watch the resulting `CI` workflow run to completion (`gh run watch <run-id> --exit-status`), then confirm via `gh api repos/mgnslndh/Attributary/actions/runs/<run-id>/artifacts` that a `compliance-bundle` artifact was produced alongside the existing `nuget-package` artifact, and spot-check its contents match what Task 3's local verification found.

---

## Self-Review Notes

- **Spec coverage:** §3.1 (GitHub auth fix) → Task 1. §3.2+§3.3 (SBOM + compliance generation) → Task 2. §3.4 (Dogfood task itself) → Task 2. §3.5 (Pack depends on Dogfood) → Task 3 Step 1. §3.6 (embedding in the nupkg) → Task 3 Step 2. §3.7 (CI artifact upload) → Task 4.
- **Type/interface consistency:** `GitHubVcsSource`'s new `token` parameter is optional with a default of `null`, so every other existing call site (if any beyond `GenerateRunner.cs`) keeps compiling unchanged — checked, `GenerateRunner.cs` is the only call site (confirmed via the same repo read that produced Task 1's diffs). `DogfoodTask`'s output paths (`artifacts/sbom/attributary.cdx.json`, `artifacts/compliance`) are the exact literal strings Task 3's `.csproj` `Condition="Exists(...)"` checks and Task 4's upload-artifact `path:` globs reference — verified they match character-for-character across all three tasks' code.
- **No placeholder scan:** no TBD/TODO; the one carried-forward risk from the spec (no policy denial expected but not yet verified) has an explicit verification step (Task 2 Step 4) rather than being silently assumed.
