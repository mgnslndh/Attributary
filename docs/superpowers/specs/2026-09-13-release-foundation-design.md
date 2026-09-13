# Release Foundation — Design Spec

Date: 2026-09-13
Status: Approved for planning

## 1. Purpose

This is sub-project 1 of a larger initiative to bring Attributary to a
releasable, modern .NET open source standard. The full initiative was
decomposed into an ordered sequence:

1. **Release Foundation** (this spec) — build orchestration, versioning,
   central package management, packaging metadata, code style, SDK pinning.
2. CI pipeline (GitHub Actions), built on top of the same local build.
3. Dogfooding — generate Attributary's own SBOM and run Attributary on it,
   committing the resulting compliance artifacts and checking them in CI.
4. Governance docs (`CONTRIBUTING`, `CODE_OF_CONDUCT`, `SECURITY`,
   `CHANGELOG`) and release automation (tag → build → GitHub Release).
5. NuGet.org publish — explicitly gated on the user's separate approval;
   not started until requested.

This spec covers only sub-project 1: giving the repo a single build entry
point that runs identically locally and in CI, a git-tag-driven versioning
scheme, centrally managed package versions, correct NuGet packaging
metadata (packing readiness only — no publish), consistent code style, and
a pinned SDK.

## 2. Non-goals (this sub-project)

- **No GitHub Actions workflow yet.** CI wiring is sub-project 2; this spec
  only produces the build that CI will later invoke.
- **No dogfooding.** Attributary is not yet run against its own
  dependencies here; that's sub-project 3.
- **No NuGet.org publish.** The `Publish` build task exists as a defined,
  gated target but is not wired to run automatically and requires the
  user's explicit go-ahead in a later sub-project.
- **No governance docs** (`CONTRIBUTING.md`, etc.) — sub-project 4.
- **No public library packages.** Only `Attributary.Cli` is packable; the
  other seven `src/` projects are implementation details and are marked
  `IsPackable=false`.

## 3. Components

### 3.1 Cake Frosting build project

A new `build/Attributary.Build/` console project (referencing
`Cake.Frosting`), outside the `src`/`tests` split, not added to
`Attributary.sln` as a buildable-by-default configuration that interferes
with normal IDE builds. Tasks, in dependency order:

- `Clean` — removes `bin`/`obj` under `src`, `tests`, and `build`.
- `Restore` — `dotnet restore` on `Attributary.sln`.
- `Build` — `dotnet build --no-restore`, with `TreatWarningsAsErrors=true`
  for this invocation (see §3.5).
- `Format-Check` — `dotnet format --verify-no-changes`.
- `Audit` — `dotnet list package --vulnerable --include-transitive`
  against every project; fails the task if any vulnerable package is
  reported. See §3.6.
- `Test` — runs each `*.Tests.csproj` (via `dotnet test`, Microsoft
  Testing Platform), collecting coverage; incorporates the logic currently
  in `scripts/Get-CodeCoverage.ps1` rather than keeping it as a
  separately-invoked script.
- `Pack` — `dotnet pack` on `Attributary.Cli` only.
- `Publish` — defined but not runnable without an explicit opt-in flag
  and API key; out of scope to actually wire up in this sub-project.

Root-level `build.ps1` and `build.sh` bootstrap scripts do
`dotnet run --project build/Attributary.Build -- <target>`, so a
contributor and CI invoke the exact same entry point. Default target
(no args) runs `Restore` → `Build` → `Format-Check` → `Audit` → `Test`.

### 3.2 Versioning — MinVer

`MinVer` is added as a central package (§3.3) and referenced from the root
`Directory.Build.props`, so **every** project in the solution — not just
`Attributary.Cli` — gets `Version`, `AssemblyVersion`, `FileVersion`, and
`InformationalVersion` derived from `git describe`. This means
`attributary --version` and any DLL's file properties are meaningful for
bug reports even though only the CLI is packed.

Convention: annotated tags of the form `v0.1.0`, `v0.2.0`, etc. (MinVer's
default `v` prefix; no config override needed). No tags exist yet in this
repo, so until the first tag is pushed, builds report a version like
`0.0.0-alpha.0.<height>` — expected pre-release behavior, not a bug.

The hardcoded `<Version>0.1.0</Version>` in `Attributary.Cli.csproj` is
removed.

### 3.3 Central Package Management

Root `Directory.Packages.props` with `ManagePackageVersionsCentrally=true`,
pinning:

| Package | Version |
|---|---|
| `Microsoft.Extensions.DependencyInjection` | 10.0.12 |
| `Spectre.Console` | 0.57.2 |
| `Spectre.Console.Cli` | 0.55.0 |
| `Spectre.Console.Testing` | 0.57.2 |
| `YamlDotNet` | 18.1.0 |
| `CycloneDX.Core` | 12.1.2 |
| `TUnit` | 1.67.0 |
| `MinVer` | latest stable at implementation time |
| `Microsoft.SourceLink.GitHub` | latest stable at implementation time |

Every `.csproj`'s `PackageReference` elements drop their `Version`
attribute; versions live only in `Directory.Packages.props`.

### 3.4 Packaging metadata (`Attributary.Cli.csproj`)

Packing-readiness only — no publish step is invoked.

- `Description` — drawn from the README's opening sentence.
- `Authors` — Magnus Lindhe.
- `PackageProjectUrl` / `RepositoryUrl` —
  `https://github.com/mgnslndh/Attributary`.
- `PackageLicenseExpression` — `MIT`.
- `PackageReadmeFile` — embeds the repo `README.md` in the package.
- `PackageTags` — `sbom`, `cyclonedx`, `spdx`, `license-compliance`,
  `oss-compliance`, `attribution`, `notice`, `third-party-notices`,
  `supply-chain`, `dotnet-tool` (validated against comparable packages
  already on nuget.org: CycloneDX tool packages, `spdx`, `ThirdLicense`,
  `ThirdPartyNoticesGenerator`).
- `PackageReleaseNotes` — placeholder pointing at `CHANGELOG.md`, which is
  created in sub-project 4; left as a short static string until then.
- Deterministic/reproducible build flags: `Deterministic=true`,
  `ContinuousIntegrationBuild` set from a `CI` environment variable,
  `EmbedUntrackedSources=true`, `PublishRepositoryUrl=true`.
- `IncludeSymbols=true`, `SymbolPackageFormat=snupkg`.
- `Microsoft.SourceLink.GitHub` package reference so the published package
  is source-debuggable.

The seven non-CLI `src/` projects (`Domain`, `Rules`, `Resolution`, `Sbom`,
`Artifacts`, `Output`, `Diagnostics`) get `IsPackable=false` in a shared
`src/Directory.Build.props` (importing the root one) so a solution-wide
`dotnet pack` cannot accidentally emit stray `.nupkg`s for them.

### 3.5 Code style

Root `.editorconfig`: the standard `dotnet new editorconfig` baseline plus
C# conventions consistent with the `Nullable`/`ImplicitUsings` settings
already in `Directory.Build.props`. `TreatWarningsAsErrors=true` is applied
only for the build configuration the Cake `Build` task uses (a `Release`-
style invocation), not for default IDE/F5 builds, so day-to-day editing
isn't blocked by transient warnings while CI and the local `build.ps1`
entry point still enforce a clean build.

### 3.6 Vulnerability gate (Cyber Resilience Act–aligned practice)

Two layers, in the spirit of CRA vulnerability-handling expectations even
though the Act's obligations don't formally apply to a non-commercial OSS
maintainer:

1. **SDK-level, always on**: `Directory.Build.props` sets
   `NuGetAudit=true`, `NuGetAuditMode=all` (audits transitive dependencies,
   not just direct ones), `NuGetAuditLevel=low`. This surfaces known-
   vulnerability warnings (NU1901–NU1904) on every `restore`/`build`, not
   just in CI.
2. **Explicit gate**: the Cake `Audit` task (§3.1) runs
   `dotnet list package --vulnerable --include-transitive` and fails the
   task if anything is reported. `Audit` is a hard prerequisite of
   `Publish`, so a release can never ship with a known-vulnerable
   dependency — mirroring, informally, the vulnerability-handling posture
   the CRA expects of products with digital elements. Full CRA alignment
   also depends on sub-project 3 (SBOM) and sub-project 4
   (`SECURITY.md` disclosure process); this task only covers the
   build-time gate.

### 3.7 SDK pinning

`global.json` gains an `sdk` section pinning `10.0.401` (the version
installed in this environment) with `rollForward: latestFeature`, in
addition to the existing `test.runner` entry. This makes local builds and
CI resolve the same SDK deterministically instead of "whatever is newest
on `PATH`."

## 4. Testing

- `./build.ps1` (default target) reproduces what `dotnet build` +
  `dotnet test` do today, plus the new `Format-Check` and `Audit` gates.
- `./build.ps1 pack` produces a versioned package (e.g.
  `Attributary.0.0.0-alpha.0.<height>.nupkg` pre-tag, or
  `Attributary.0.1.0.nupkg` once a `v0.1.0` tag exists) with an
  accompanying `.snupkg`.
- Confirm `dotnet build` still works directly (without going through Cake)
  for contributors who prefer it — the Cake project only adds
  orchestration, it doesn't become the only way to build.
- Confirm `attributary --version` (or equivalent CLI output) reflects the
  MinVer-derived version once wired.

## 5. Risks / open questions carried forward

- Exact `MinVer`/`Microsoft.SourceLink.GitHub` versions are resolved at
  implementation time (latest stable), not pinned in this spec.
- `PackageReleaseNotes` content is a placeholder until `CHANGELOG.md`
  exists (sub-project 4); the implementation plan should keep this simple
  rather than inventing changelog infrastructure early.
- The exact coverage-collection mechanics currently in
  `scripts/Get-CodeCoverage.ps1` need to be read and ported into the Cake
  `Test` task rather than assumed; the implementation plan should treat
  this as a "read before porting" step.
