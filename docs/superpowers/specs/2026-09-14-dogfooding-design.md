# Dogfooding — Design Spec

Date: 2026-09-14
Status: Approved for planning

## 1. Purpose

This is sub-project 3 of the release-readiness initiative (sub-project 1,
"Release Foundation," and sub-project 2, "CI Pipeline," are both complete
and merged to `main`). It makes Attributary run itself against its own
real dependency tree as part of every build: generate a CycloneDX SBOM for
`Attributary.Cli`'s actual shipped dependencies, run `attributary generate`
against it, and fail the build if the tool can't cleanly process its own
output. The generated SBOM and compliance bundle are also embedded inside
the published `.nupkg` and uploaded as a browsable CI artifact — so the
tool ships proof of its own compliance, and anyone can inspect it without
running anything themselves.

This spec went through substantial revision during brainstorming. The
original idea — commit the generated SBOM/compliance artifacts to the repo
and run a CI drift check comparing regenerated output against the
committed copy — was reconsidered and dropped. Section 2 records why.

## 2. Non-goals (this sub-project)

- **No committed SBOM or compliance artifacts.** They are fully derived
  from files already in git (`Directory.Packages.props`, project
  references, `DefaultRules.yaml`) — build output, not source of truth,
  same category as `bin/`, `obj/`, and the `.nupkg`/`.snupkg` themselves.
  Committing them and then having to keep them in sync would create a
  staleness problem that doesn't otherwise exist: every regeneration would
  need diffing against the committed copy, which in turn would need
  volatile-field normalization (the CycloneDX tool embeds a random
  `serialNumber` and a `metadata.timestamp` that both change on every
  single run — confirmed by running the tool twice back to back and
  diffing the output), a warning/error severity split for Dependabot
  noise, and a whole CI drift-check mechanism. None of that is needed if
  the artifacts are never committed in the first place. Instead, the real
  correctness signal — "does Attributary process its own dependency tree
  without errors" — comes from the tool actually succeeding in CI, exactly
  like `Test` doesn't commit `TestResults/` to prove tests pass.
- **No release-artifact wiring.** Attaching the SBOM/compliance bundle to
  a *GitHub Release* specifically (as opposed to a CI run's own artifact
  list, which this sub-project does cover) is sub-project 4's concern
  (release automation), since it depends on the tagging/release workflow
  that doesn't exist yet.
- **No NuGet.org publishing.** Unrelated to this sub-project; remains
  gated on the user's separate explicit approval (sub-project 5).
- **No auto-regenerate-and-push-back automation for Dependabot PRs.**
  Moot — there is nothing to regenerate-and-push-back, since nothing is
  committed. A Dependabot PR that bumps a shipped dependency to a version
  with a genuinely problematic license will simply fail this sub-project's
  CI check like any other PR would, which is the correct, valuable
  behavior, not friction to work around.

## 3. Components

### 3.1 GitHub API authentication fix (prerequisite)

`GitHubVcsSource` (`src/Attributary.Resolution/Sources/GitHubVcsSource.cs`)
currently calls `httpClient.GetAsync($"https://api.github.com/repos/{owner}/{repo}/license", ct)`
with no `Authorization` header — an unauthenticated GitHub API rate limit
of 60 requests/hour, shared across whatever else is running on a CI
runner's IP range. `Attributary.Cli`'s real dependency tree needs one
license lookup per GitHub-hosted component, and CI runners are ephemeral
(no persistent cache between runs), so cold dogfood runs are a real risk
of hitting that limit and producing unreliable diagnostics.

Fix: `GitHubVcsSource` gains an optional `string? token` constructor
parameter. When set, it's applied as an `Authorization: Bearer <token>`
header on an explicitly-constructed `HttpRequestMessage` for its own
GitHub API call only — **never** on `HttpClient.DefaultRequestHeaders`,
since that same `HttpClient` instance is shared with `SbomLicenseUrlSource`
(`src/Attributary.Cli/Pipeline/GenerateRunner.cs:63`), which fetches
arbitrary third-party URLs read from SBOM data. Setting the header on the
shared client's defaults would leak the GitHub token to whatever host an
SBOM's `license.url` field happens to point at — a real credential-leak
bug, not a hypothetical one. `GenerateRunner.BuildSources` reads
`Environment.GetEnvironmentVariable("GITHUB_TOKEN")` and passes it through
to `GitHubVcsSource`'s constructor. A new unit test in
`GitHubVcsSourceTests.cs` verifies the header is set correctly when a
token is provided and confirms it is never applied to any other source's
requests.

### 3.2 SBOM generation

The `CycloneDX` dotnet tool (verified against its actual installed
`--help` output, not assumed from docs) is added to
`.config/dotnet-tools.json`, pinned at `6.2.0` (the current version,
confirmed via `dotnet tool list`). Invoked as a local tool via
`dotnet tool run CycloneDX ...` (matching the existing pattern already
used for `reportgenerator` in `TestTask.cs`, rather than the bare
`dotnet CycloneDX` form that only works reliably for tools resolvable on
`PATH`).

Command, run against `Attributary.Cli`'s own project file — recursively
following its internal project references (`Domain`, `Rules`,
`Resolution`, `Sbom`, `Artifacts`, `Output`, `Diagnostics`) rather than
the whole solution, so the SBOM reflects exactly what ships when someone
installs the dotnet tool, not test-only or build-tooling dependencies
(`TUnit`, `Cake.*`) nobody actually receives:

```
dotnet tool run CycloneDX -- src/Attributary.Cli/Attributary.Cli.csproj \
  -o artifacts/sbom -fn attributary.cdx.json -rs -F Json -ed -c Release -ns
```

- `-rs` (`--recursive`) — follow `Attributary.Cli`'s project references.
- `-F Json` — JSON output (matches what `attributary generate --sbom`
  consumes; confirmed via the README this repo already documents JSON as
  the expected input format).
- `-ed` (`--exclude-dev`) — excludes any `DevelopmentDependency`-flagged
  packages (e.g. analyzers) that never ship in the actual assembly.
- `-c Release` — matches the configuration the rest of the pipeline
  builds with, so conditional `PackageReference`s resolve the same way.
- `-ns` (`--no-serial-number`) — omits the random UUID CycloneDX would
  otherwise embed. No longer load-bearing for correctness (nothing
  diffs across runs — see §2), but keeps the artifact itself
  deterministic given identical inputs, which is a reasonable property on
  its own.
- Deliberately **not** using `-egl`/`-gbt` (CycloneDX's own optional
  GitHub-based license resolution) — the SBOM should stay a "bare"
  component/dependency graph (PURLs, versions, whatever nuspec-declared
  license expressions already exist) so that Attributary's own resolution
  chain (§3.1's fix included) does all the enrichment work. That's the
  actual point of dogfooding: exercising Attributary's resolution logic,
  not CycloneDX's competing one.

Output: `artifacts/sbom/attributary.cdx.json`. `artifacts/` is already
listed in `.gitignore` (confirmed) — no new ignore rules needed.

### 3.3 Compliance bundle generation

The already-built `Attributary.Cli.dll` (from the `Build` task this new
task depends on — no separate `dotnet run` re-build) is invoked directly:

```
dotnet src/Attributary.Cli/bin/Release/net10.0/Attributary.Cli.dll generate \
  --sbom artifacts/sbom/attributary.cdx.json \
  --out artifacts/compliance \
  --format md
```

`--format md` (rather than the tool's own default `txt`) so
`COMPLIANCE-REPORT.md` and `THIRD-PARTY-NOTICES.md` render directly on
GitHub when browsed from a CI run's uploaded artifact or, later, a
release page. Per-license text files under `LICENSES/` are always `.txt`
regardless of `--format` (raw license text, not a report Attributary
itself formats). Compliance report writers do not embed a generation
timestamp (confirmed by reading `TxtComplianceReportWriter.cs` and
`MdComplianceReportWriter.cs` directly) — output is fully deterministic
given the same SBOM + rules, consistent with not needing any drift
handling.

### 3.4 `Dogfood` Cake task

New `build/Attributary.Build/Tasks/DogfoodTask.cs`, `[IsDependentOn(typeof(BuildTask))]`.
Runs §3.2 then §3.3 in sequence via `StartProcess` (matching the existing
pattern in `AuditTask.cs`/`TestTask.cs`). The task fails
(`throw new CakeException(...)`) if either process exits non-zero — most
importantly, if `attributary generate` itself reports a real diagnostic
error (an unresolved component, a denied license), that is a genuine,
current compliance problem with the tool's own dependencies, not a
staleness concern, and must fail the build regardless of context (no
severity split, no Dependabot special-casing — §2 explains why none of
that machinery is needed here).

### 3.5 `Pack` depends on `Dogfood`

`build/Attributary.Build/Tasks/PackTask.cs` changes from
`[IsDependentOn(typeof(BuildTask))]` to
`[IsDependentOn(typeof(DogfoodTask))]` (which itself still depends on
`Build`, so the effective chain is unchanged except for the new step in
between). This makes the guarantee concrete: **no package can be produced
without a successful dogfood run first.** It also guarantees the
`artifacts/sbom/` and `artifacts/compliance/` files exist on disk before
`dotnet pack` runs, which §3.6 depends on.

### 3.6 Embedding the bundle in the `.nupkg`

`src/Attributary.Cli/Attributary.Cli.csproj` gains:

```xml
<ItemGroup>
  <None Include="..\..\artifacts\sbom\attributary.cdx.json"
        Pack="true" PackagePath="sbom\"
        Condition="Exists('..\..\artifacts\sbom\attributary.cdx.json')" />
  <None Include="..\..\artifacts\compliance\**\*"
        Pack="true" PackagePath="compliance\"
        Condition="Exists('..\..\artifacts\compliance')" />
</ItemGroup>
```

The `Condition="Exists(...)"` guards mean a bare `dotnet build` or
`dotnet pack` run without the `Dogfood` task having executed first (e.g.
someone packing manually outside the Cake pipeline) doesn't fail — it
just produces a package without the bundle, rather than erroring. Under
the normal `./build.ps1 Pack` / `./build.sh Pack` path (§3.5), the files
always exist by the time packing happens, so the bundle is always
included in the artifact CI and the Cake pipeline actually produce.

### 3.7 CI artifact upload

No new CI *step* is needed — `Pack` already runs unconditionally on the
`ubuntu-latest` leg of every CI run (`ci.yml`, `if: runner.os == 'Linux'`),
and now transitively runs `Dogfood` first (§3.5), so this sub-project gets
full CI coverage on every push and PR for free.

What does need to change: the existing `Upload package artifact` step
only uploads `artifacts/*.nupkg`/`artifacts/*.snupkg`. Add a second,
separate `actions/upload-artifact@v7` step named `compliance-bundle`
uploading `artifacts/sbom/**` and `artifacts/compliance/**` directly (not
only inside the `.nupkg`), so anyone can browse the SBOM and compliance
report straight from the Actions run UI without unzipping a package
first. Same `if-no-files-found: error` as the existing upload step — if
`Dogfood`/`Pack` succeeded, these files must exist.

## 4. Testing

- `GitHubVcsSourceTests.cs`: new test confirming the `Authorization`
  header is set correctly when a token is supplied to `GitHubVcsSource`,
  and that no other source (`SbomLicenseUrlSource`'s tests, or a shared
  `HttpClient` fixture) ever receives it.
- Local: `./build.ps1 Dogfood` (or `./build.sh Dogfood`) run directly,
  confirming it produces `artifacts/sbom/attributary.cdx.json` and a full
  `artifacts/compliance/` bundle, and exits 0 against this repo's real,
  current dependency tree — this is the first real end-to-end proof this
  spec's design actually works, not merely command syntax verified in
  isolation.
- Local: `./build.ps1 Pack` (or `./build.sh Pack`), confirming the
  produced `.nupkg` contains `sbom/attributary.cdx.json` and the
  `compliance/` tree (inspectable by renaming to `.zip` and extracting,
  or `unzip -l`).
- CI: after merge and push, confirm the `compliance-bundle` artifact
  appears on the CI run and is downloadable/browsable, alongside the
  existing `nuget-package` artifact.

## 5. Risks / open questions carried forward

- This repo's actual dependencies (Spectre.Console, YamlDotNet,
  CycloneDX.Core, MinVer, Cake.Frosting/Common/MinVer,
  Microsoft.Extensions.DependencyInjection, Microsoft.SourceLink.GitHub)
  are all well-known permissively-licensed (MIT/Apache-2.0-style)
  packages, and the bundled `DefaultRules.yaml` already covers both
  licenses with an `allow` policy — no policy denial is expected on the
  first real run, but this has not been verified end-to-end yet (see
  §4's local-testing step, which is where that gets proven for real
  during implementation, not assumed here).
- `-c Release` is passed to the CycloneDX tool to match the rest of the
  pipeline's configuration; if `BuildContext.Configuration` is ever
  changed from its `"Release"` default via the `--configuration` Cake
  argument, this hardcoded value would need to become a variable instead
  — not addressed now since nothing in this repo currently overrides that
  default, but worth flagging for the implementer.
