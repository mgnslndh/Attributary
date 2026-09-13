# Release Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Attributary a single build entry point (Cake Frosting) that runs identically locally and in CI, git-tag-driven versioning (MinVer) across every project, centrally managed package versions, correct-but-unpublished NuGet packaging metadata, a vulnerability audit gate, consistent code style, and a pinned SDK.

**Architecture:** Five sequential tasks, each independently buildable/testable: (1) central package management + SDK pin, (2) MinVer versioning + NuGet audit properties, (3) packaging metadata + `IsPackable=false` on internal libraries, (4) `.editorconfig`, (5) the Cake Frosting build project that wires all of the above into `Clean`/`Restore`/`Build`/`Format-Check`/`Audit`/`Test`/`Pack` tasks plus `build.ps1`/`build.sh` bootstrap scripts. Later tasks build on earlier ones (Task 5's `Build` task references the `TreatWarningsAsErrors` convention from Task 4's rationale; its `Pack` task packs the project made packable-correctly in Task 3).

**Tech Stack:** .NET 10 SDK (10.0.401, pinned), MSBuild central package management, MinVer 8.0.0, Cake.Frosting 6.2.0 + Cake.Common 6.2.0 + Cake.MinVer 4.0.0, Microsoft.SourceLink.GitHub 10.0.401.

**Spec:** [`docs/superpowers/specs/2026-09-13-release-foundation-design.md`](../specs/2026-09-13-release-foundation-design.md)

## Global Constraints

- Target framework for all projects: `net10.0` (already set in root `Directory.Build.props`; do not change).
- SDK pin: `10.0.401`, `rollForward: latestFeature` in `global.json`.
- MinVer version: `8.0.0`. Tag prefix: `v` (via `<MinVerTagPrefix>v</MinVerTagPrefix>` — MinVer's own default prefix is empty, so this must be set explicitly).
- Cake.Frosting / Cake.Common version: `6.2.0` (must match each other).
- Cake.MinVer version: `4.0.0`. Its `TagPrefix` setting must be `"v"`, matching `Directory.Build.props`'s `MinVerTagPrefix` — both read the same git history via the same underlying MinVer algorithm, so a mismatched prefix would make the Cake-logged version disagree with the actually-packed version.
- Microsoft.SourceLink.GitHub version: `10.0.401`.
- Only `Attributary.Cli` is packable (`IsPackable=true`); every other `src/` project is `IsPackable=false`.
- `PackageTags`: `sbom;cyclonedx;spdx;license-compliance;oss-compliance;attribution;notice;third-party-notices;supply-chain;dotnet-tool`.
- Repo GitHub URL: `https://github.com/mgnslndh/Attributary`.
- All commands in this plan assume the current working directory is the repo root (`C:\Dev\GitHub\mgnslndh\Attributary`).
- No task in this plan wires up `dotnet nuget push` / NuGet.org publishing. The `Pack` task only produces local artifacts.

---

### Task 1: Central Package Management + SDK pinning

**Files:**
- Create: `Directory.Packages.props`
- Modify: `global.json`
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj`
- Modify: `src/Attributary.Rules/Attributary.Rules.csproj`
- Modify: `src/Attributary.Sbom/Attributary.Sbom.csproj`
- Modify: `tests/Attributary.Artifacts.Tests/Attributary.Artifacts.Tests.csproj`
- Modify: `tests/Attributary.Cli.Tests/Attributary.Cli.Tests.csproj`
- Modify: `tests/Attributary.Diagnostics.Tests/Attributary.Diagnostics.Tests.csproj`
- Modify: `tests/Attributary.Output.Tests/Attributary.Output.Tests.csproj`
- Modify: `tests/Attributary.Resolution.Tests/Attributary.Resolution.Tests.csproj`
- Modify: `tests/Attributary.Rules.Tests/Attributary.Rules.Tests.csproj`
- Modify: `tests/Attributary.Sbom.Tests/Attributary.Sbom.Tests.csproj`

**Interfaces:**
- Produces: `Directory.Packages.props` at repo root with `ManagePackageVersionsCentrally=true` — every later task that adds a new NuGet dependency adds one `<PackageVersion>` entry here.

- [ ] **Step 1: Create `Directory.Packages.props`**

```xml
<Project>

  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
    <PackageVersion Include="Spectre.Console" Version="0.57.2" />
    <PackageVersion Include="Spectre.Console.Cli" Version="0.55.0" />
    <PackageVersion Include="Spectre.Console.Testing" Version="0.57.2" />
    <PackageVersion Include="YamlDotNet" Version="18.1.0" />
    <PackageVersion Include="CycloneDX.Core" Version="12.1.2" />
    <PackageVersion Include="TUnit" Version="1.67.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Edit `global.json` to pin the SDK**

Current content:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

New content:

```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature"
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

- [ ] **Step 3: Strip `Version` attributes from every `PackageReference`**

In `src/Attributary.Cli/Attributary.Cli.csproj`, change:

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.12" />
    <PackageReference Include="Spectre.Console" Version="0.57.2" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.55.0" />
```

to:

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Spectre.Console" />
    <PackageReference Include="Spectre.Console.Cli" />
```

In `src/Attributary.Rules/Attributary.Rules.csproj`, change:

```xml
    <PackageReference Include="YamlDotNet" Version="18.1.0" />
```

to:

```xml
    <PackageReference Include="YamlDotNet" />
```

In `src/Attributary.Sbom/Attributary.Sbom.csproj`, change:

```xml
    <PackageReference Include="CycloneDX.Core" Version="12.1.2" />
```

to:

```xml
    <PackageReference Include="CycloneDX.Core" />
```

In each of the following files, change:

```xml
    <PackageReference Include="TUnit" Version="1.67.0" />
```

to:

```xml
    <PackageReference Include="TUnit" />
```

— `tests/Attributary.Artifacts.Tests/Attributary.Artifacts.Tests.csproj`, `tests/Attributary.Diagnostics.Tests/Attributary.Diagnostics.Tests.csproj`, `tests/Attributary.Output.Tests/Attributary.Output.Tests.csproj`, `tests/Attributary.Resolution.Tests/Attributary.Resolution.Tests.csproj`, `tests/Attributary.Rules.Tests/Attributary.Rules.Tests.csproj`, `tests/Attributary.Sbom.Tests/Attributary.Sbom.Tests.csproj`.

In `tests/Attributary.Cli.Tests/Attributary.Cli.Tests.csproj`, change both:

```xml
    <PackageReference Include="Spectre.Console.Testing" Version="0.57.2" />
    <PackageReference Include="TUnit" Version="1.67.0" />
```

to:

```xml
    <PackageReference Include="Spectre.Console.Testing" />
    <PackageReference Include="TUnit" />
```

- [ ] **Step 4: Verify restore and build still work**

Run: `dotnet restore Attributary.sln`
Expected: succeeds with no `NU1008` ("central package management" version-conflict) errors.

Run: `dotnet build Attributary.sln --no-restore`
Expected: succeeds exactly as it did before this task (no behavior change yet — only where versions are declared has changed).

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props global.json src/Attributary.Cli/Attributary.Cli.csproj src/Attributary.Rules/Attributary.Rules.csproj src/Attributary.Sbom/Attributary.Sbom.csproj tests/Attributary.Artifacts.Tests/Attributary.Artifacts.Tests.csproj tests/Attributary.Cli.Tests/Attributary.Cli.Tests.csproj tests/Attributary.Diagnostics.Tests/Attributary.Diagnostics.Tests.csproj tests/Attributary.Output.Tests/Attributary.Output.Tests.csproj tests/Attributary.Resolution.Tests/Attributary.Resolution.Tests.csproj tests/Attributary.Rules.Tests/Attributary.Rules.Tests.csproj tests/Attributary.Sbom.Tests/Attributary.Sbom.Tests.csproj
git commit -m "build: adopt central package management and pin SDK version"
```

---

### Task 2: MinVer versioning + NuGet audit gate (SDK-level)

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `Directory.Build.props`
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj`

**Interfaces:**
- Consumes: `Directory.Packages.props` from Task 1 (adds one more `PackageVersion` entry to it).
- Produces: every project in the solution gets `Version`/`AssemblyVersion`/`FileVersion`/`InformationalVersion` derived from `git describe` once this task lands — later tasks (and CI) can rely on `dotnet msbuild <project> -getProperty:Version` returning a real value instead of a hardcoded one.

- [ ] **Step 1: Add the MinVer package version**

In `Directory.Packages.props`, add to the existing `<ItemGroup>`:

```xml
    <PackageVersion Include="MinVer" Version="8.0.0" />
```

- [ ] **Step 2: Wire MinVer and the NuGet audit properties into the root `Directory.Build.props`**

Current content:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

New content:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>

    <MinVerTagPrefix>v</MinVerTagPrefix>

    <NuGetAudit>true</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <NuGetAuditLevel>low</NuGetAuditLevel>

    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MinVer" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Remove the hardcoded version from `Attributary.Cli.csproj`**

Change:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>attributary</ToolCommandName>
    <PackageId>Attributary</PackageId>
    <Version>0.1.0</Version>
  </PropertyGroup>
```

to:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>attributary</ToolCommandName>
    <PackageId>Attributary</PackageId>
  </PropertyGroup>
```

- [ ] **Step 4: Verify MinVer is producing versions solution-wide**

Run: `dotnet restore Attributary.sln`
Expected: succeeds; MinVer restores without needing any git tags (no tags exist yet in this repo).

Run: `dotnet msbuild src/Attributary.Cli/Attributary.Cli.csproj -getProperty:Version`
Expected: prints something of the form `0.0.0-alpha.0.<N>` (MinVer's height-based pre-release version — there are no tags yet, so this is correct, not an error).

Run: `dotnet msbuild src/Attributary.Domain/Attributary.Domain.csproj -getProperty:Version`
Expected: prints the **same** version string as the `Attributary.Cli` command above — confirms MinVer is applied solution-wide via `Directory.Build.props`, not just to the packed CLI project.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props Directory.Build.props src/Attributary.Cli/Attributary.Cli.csproj
git commit -m "build: adopt MinVer versioning and enable NuGet vulnerability audits"
```

---

### Task 3: Packaging metadata + `IsPackable=false` on internal libraries

**Files:**
- Create: `src/Directory.Build.props`
- Modify: `Directory.Packages.props`
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj`

**Interfaces:**
- Consumes: `Directory.Packages.props` from Tasks 1–2 (adds a `Microsoft.SourceLink.GitHub` entry).
- Produces: `dotnet pack src/Attributary.Cli/Attributary.Cli.csproj` now yields a correctly-described, source-linked, symbol-carrying package; `dotnet pack` on any other `src/` project yields nothing (by design).

- [ ] **Step 1: Add the SourceLink package version**

In `Directory.Packages.props`, add to the existing `<ItemGroup>`:

```xml
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.401" />
```

- [ ] **Step 2: Create `src/Directory.Build.props`**

```xml
<Project>

  <Import Project="$(MSBuildThisFileDirectory)..\Directory.Build.props" />

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

</Project>
```

This applies to every project under `src/`, including `Attributary.Cli` — Step 3 explicitly re-enables it for the CLI, since a project-level property always wins over the one set in its `Directory.Build.props`.

- [ ] **Step 3: Add packaging metadata to `Attributary.Cli.csproj`**

Change:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>attributary</ToolCommandName>
    <PackageId>Attributary</PackageId>
  </PropertyGroup>

</Project>
```

to:

```xml
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>attributary</ToolCommandName>
    <PackageId>Attributary</PackageId>
    <IsPackable>true</IsPackable>

    <Description>Attributary is a .NET CLI tool that reads a CycloneDX SBOM and generates the artifacts you need to be open source license compliant when distributing a piece of software: per-license license files, an aggregated NOTICE file, a third-party attribution document, and a compliance report -- in text, Markdown, JSON, or HTML.</Description>
    <Authors>Magnus Lindhe</Authors>
    <PackageProjectUrl>https://github.com/mgnslndh/Attributary</PackageProjectUrl>
    <RepositoryUrl>https://github.com/mgnslndh/Attributary</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>sbom;cyclonedx;spdx;license-compliance;oss-compliance;attribution;notice;third-party-notices;supply-chain;dotnet-tool</PackageTags>
    <PackageReleaseNotes>See https://github.com/mgnslndh/Attributary/blob/main/CHANGELOG.md</PackageReleaseNotes>

    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
  </ItemGroup>

</Project>
```

(The `<Version>` property stays removed from Task 2 — MinVer supplies it.)

- [ ] **Step 4: Verify packing behavior**

Run: `dotnet restore Attributary.sln`
Expected: succeeds.

Run: `dotnet pack src/Attributary.Cli/Attributary.Cli.csproj -c Release -o artifacts-test`
Expected: succeeds, producing both `artifacts-test/Attributary.<version>.nupkg` and `artifacts-test/Attributary.<version>.snupkg`.

Run: `dotnet pack src/Attributary.Domain/Attributary.Domain.csproj -c Release -o artifacts-test`
Expected: succeeds with exit code 0, but produces **no** new file in `artifacts-test` (confirms `IsPackable=false` is now in effect via the file, not just a one-off `-p:` override).

Run: `Remove-Item -Recurse -Force artifacts-test` (PowerShell) or `rm -rf artifacts-test` (bash) to clean up the scratch output directory — it is not meant to be committed.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props src/Directory.Build.props src/Attributary.Cli/Attributary.Cli.csproj
git commit -m "build: add NuGet packaging metadata and mark internal libraries non-packable"
```

---

### Task 4: `.editorconfig`

**Files:**
- Create: `.editorconfig`

- [ ] **Step 1: Create the root `.editorconfig`**

```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
trim_trailing_whitespace = true
indent_style = space

[*.cs]
indent_size = 4

[*.{yaml,yml,json,md}]
indent_size = 2

# .NET code style rules
[*.cs]
dotnet_sort_system_directives_first = true
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion
dotnet_style_require_accessibility_modifiers = for_non_interface_members:suggestion
dotnet_style_predefined_type_for_locals_parameters_members = true:suggestion
dotnet_style_predefined_type_for_member_access = true:suggestion
dotnet_style_object_initializer = true:suggestion
dotnet_style_collection_initializer = true:suggestion
dotnet_style_prefer_auto_properties = true:suggestion
dotnet_style_coalesce_expression = true:suggestion
dotnet_style_null_propagation = true:suggestion

# C# code style rules
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
csharp_style_expression_bodied_methods = when_on_single_line:suggestion
csharp_style_expression_bodied_properties = true:suggestion
csharp_prefer_braces = true:suggestion
csharp_new_line_before_open_brace = all
csharp_new_line_before_else = true
csharp_new_line_before_catch = true
csharp_new_line_before_finally = true
csharp_indent_case_contents = true
csharp_indent_switch_labels = true
csharp_space_after_cast = false
csharp_space_between_method_call_parameter_list_parentheses = false
```

Note: `end_of_line = crlf` matches the line endings this repo already uses (confirmed by the `LF will be replaced by CRLF` warning git prints on commit in this environment) — do not change it to `lf` without checking with the user first, since that would rewrite every existing file's line endings.

- [ ] **Step 2: Verify `dotnet format` reports no violations**

Run: `dotnet format --verify-no-changes`
Expected: exit code `0`, no output (the codebase was already format-clean before this `.editorconfig` existed, per a pre-implementation check — this file only makes those same conventions explicit and enforceable).

If it unexpectedly reports violations: run `dotnet format` (without `--verify-no-changes`) to auto-fix them, review the diff, then re-run `dotnet format --verify-no-changes` to confirm it now passes before proceeding.

- [ ] **Step 3: Commit**

```bash
git add .editorconfig
git commit -m "style: add root .editorconfig"
```

---

### Task 5: Cake Frosting build project + bootstrap scripts

**Files:**
- Create: `build/Attributary.Build/Attributary.Build.csproj`
- Create: `build/Attributary.Build/Program.cs`
- Create: `build/Attributary.Build/BuildContext.cs`
- Create: `build/Attributary.Build/Tasks/CleanTask.cs`
- Create: `build/Attributary.Build/Tasks/RestoreTask.cs`
- Create: `build/Attributary.Build/Tasks/BuildTask.cs`
- Create: `build/Attributary.Build/Tasks/FormatCheckTask.cs`
- Create: `build/Attributary.Build/Tasks/AuditTask.cs`
- Create: `build/Attributary.Build/Tasks/TestTask.cs`
- Create: `build/Attributary.Build/Tasks/PackTask.cs`
- Create: `build/Attributary.Build/Tasks/DefaultTask.cs`
- Create: `build.ps1`
- Create: `build.sh`
- Modify: `Directory.Packages.props`
- Delete: `scripts/Get-CodeCoverage.ps1`

**Interfaces:**
- Consumes: the solution file `Attributary.sln` (relative path, assumes CWD = repo root); `Attributary.Cli/Attributary.Cli.csproj`'s packing readiness from Task 3; the `dotnet-reportgenerator-globaltool` already declared in `.config/dotnet-tools.json`; the `MinVerTagPrefix=v` convention from Task 2's root `Directory.Build.props`, mirrored here via `Cake.MinVer`'s `TagPrefix` setting.
- Produces: `./build.ps1 [<TaskName>]` / `./build.sh [<TaskName>]` as the single local/CI build entry point. `TaskName` defaults to `Default`, which runs `Restore` → `Build` → `Format-Check` → `Audit` → `Test`. `BuildContext.Version` (the `Cake.MinVer`-computed version string) is available to any later task or later sub-project that needs it (e.g. a future release-automation task tagging a GitHub Release) — this task only consumes it for a startup log line.

- [ ] **Step 1: Add Cake package versions**

In `Directory.Packages.props`, add to the existing `<ItemGroup>`:

```xml
    <PackageVersion Include="Cake.Frosting" Version="6.2.0" />
    <PackageVersion Include="Cake.Common" Version="6.2.0" />
    <PackageVersion Include="Cake.MinVer" Version="4.0.0" />
```

`Cake.MinVer` lets the build project itself read the same MinVer-computed
version that `Directory.Build.props` stamps onto every assembly (Task 2),
so the build can log which version it's building. It does not change how
packages get versioned — that's still handled automatically by MSBuild via
`Directory.Build.props` — this is purely for visibility in the build's own
console output. (`#addin` directives from Cake's `.cake`-script world do
not apply here; Cake.Frosting projects consume Cake addins as ordinary
`PackageReference`s, same as `Cake.Frosting`/`Cake.Common` themselves.)

- [ ] **Step 2: Create `build/Attributary.Build/Attributary.Build.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <RootNamespace>Attributary.Build</RootNamespace>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Cake.Frosting" />
    <PackageReference Include="Cake.Common" />
    <PackageReference Include="Cake.MinVer" />
  </ItemGroup>

</Project>
```

(`TargetFramework`, `Nullable`, `ImplicitUsings` etc. are inherited from the root `Directory.Build.props`, which MSBuild finds by walking up from `build/Attributary.Build/` since there is no closer `Directory.Build.props` in that path.)

- [ ] **Step 3: Create `build/Attributary.Build/Program.cs`**

```csharp
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);
```

- [ ] **Step 4: Create `build/Attributary.Build/BuildContext.cs`**

```csharp
using Cake.Common;
using Cake.Core;
using Cake.Frosting;
using Cake.MinVer;

namespace Attributary.Build;

public sealed class BuildContext : FrostingContext
{
    public string Configuration { get; }
    public string Version { get; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Configuration = context.Argument("configuration", "Release");

        var minVer = context.MinVer(new MinVerSettings
        {
            TagPrefix = "v",
        });
        Version = minVer.Version;

        context.Information($"Building Attributary version {Version}");
    }
}
```

`TagPrefix = "v"` must match `Directory.Build.props`'s `MinVerTagPrefix`
(Task 2) exactly — both compute from the same git history via the same
MinVer algorithm, so matching settings means this logged version and the
version MSBuild actually stamps onto assemblies/packages always agree.

- [ ] **Step 5: Create `build/Attributary.Build/Tasks/CleanTask.cs`**

```csharp
using Cake.Common.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.CleanDirectories("src/**/bin");
        context.CleanDirectories("src/**/obj");
        context.CleanDirectories("tests/**/bin");
        context.CleanDirectories("tests/**/obj");
    }
}
```

- [ ] **Step 6: Create `build/Attributary.Build/Tasks/RestoreTask.cs`**

```csharp
using Cake.Common.Tools.DotNet;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Restore")]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetRestore("Attributary.sln");

        var exitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = "tool restore",
        });

        if (exitCode != 0)
        {
            throw new CakeException($"'dotnet tool restore' failed with exit code {exitCode}.");
        }
    }
}
```

- [ ] **Step 7: Create `build/Attributary.Build/Tasks/BuildTask.cs`**

```csharp
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Build")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetBuild("Attributary.sln", new DotNetBuildSettings
        {
            Configuration = context.Configuration,
            NoRestore = true,
            MSBuildSettings = new DotNetMSBuildSettings()
                .WithProperty("TreatWarningsAsErrors", "true"),
        });
    }
}
```

- [ ] **Step 8: Create `build/Attributary.Build/Tasks/FormatCheckTask.cs`**

```csharp
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Format;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Format-Check")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class FormatCheckTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetFormat("Attributary.sln", new DotNetFormatSettings
        {
            VerifyNoChanges = true,
        });
    }
}
```

- [ ] **Step 9: Create `build/Attributary.Build/Tasks/AuditTask.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Audit")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class AuditTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var exitCode = context.StartProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = "list Attributary.sln package --vulnerable --include-transitive",
                RedirectStandardOutput = true,
            },
            out IEnumerable<string> redirectedOutput);

        var outputLines = redirectedOutput.ToList();
        foreach (var line in outputLines)
        {
            context.Log.Information(line);
        }

        if (exitCode != 0)
        {
            throw new CakeException($"'dotnet list package --vulnerable' failed with exit code {exitCode}.");
        }

        var projectLines = outputLines
            .Where(line => line.TrimStart().StartsWith("The given project", StringComparison.Ordinal))
            .ToList();

        if (projectLines.Count == 0)
        {
            throw new CakeException("'dotnet list package --vulnerable' produced no per-project output; treat this as a tooling failure rather than a clean audit.");
        }

        var flaggedLines = projectLines
            .Where(line => !line.Contains("has no vulnerable packages", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (flaggedLines.Count > 0)
        {
            throw new CakeException(
                "dotnet list package --vulnerable reported vulnerable packages:" + Environment.NewLine +
                string.Join(Environment.NewLine, flaggedLines));
        }
    }
}
```

- [ ] **Step 10: Create `build/Attributary.Build/Tasks/TestTask.cs`**

This ports the merge logic from `scripts/Get-CodeCoverage.ps1` (read that file's header comment first — it explains *why* each test project is run in isolation with its own results directory rather than one solution-wide `dotnet test`, and why `reportgenerator` does the per-line coverage merge rather than hand-rolling it).

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Test")]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var testResultsRoot = context.MakeAbsolute(context.Directory("TestResults"));
        var mergedReportDir = testResultsRoot.Combine("merged");

        var testProjects = context.GetFiles("tests/**/*.csproj").ToList();
        if (testProjects.Count == 0)
        {
            throw new CakeException("No test projects found under 'tests'.");
        }

        var coverageFiles = new List<FilePath>();
        var anyTestsFailed = false;

        foreach (var project in testProjects)
        {
            var projectName = project.GetFilenameWithoutExtension().ToString();
            var projectResultsDir = testResultsRoot.Combine(projectName);

            var exitCode = context.StartProcess("dotnet", new ProcessSettings
            {
                Arguments = new ProcessArgumentBuilder()
                    .Append("test")
                    .AppendQuoted(project.FullPath)
                    .Append("--no-build")
                    .Append("--configuration").Append(context.Configuration)
                    .Append("--coverage")
                    .Append("--coverage-output-format").Append("cobertura")
                    .Append("--results-directory").AppendQuoted(projectResultsDir.FullPath),
            });

            if (exitCode != 0)
            {
                anyTestsFailed = true;
            }

            var coverageFile = context.GetFiles(projectResultsDir.FullPath + "/**/*.cobertura.xml").FirstOrDefault();
            if (coverageFile is null)
            {
                context.Warning($"No .cobertura.xml was produced for '{projectName}' under '{projectResultsDir}'.");
                continue;
            }

            coverageFiles.Add(coverageFile);
        }

        if (coverageFiles.Count == 0)
        {
            throw new CakeException("No coverage data was produced by any test project.");
        }

        if (context.DirectoryExists(mergedReportDir))
        {
            context.DeleteDirectory(mergedReportDir, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        var reportsArg = string.Join(";", coverageFiles.Select(f => f.FullPath));
        var reportGenExitCode = context.StartProcess("dotnet", new ProcessSettings
        {
            Arguments = new ProcessArgumentBuilder()
                .Append("tool").Append("run").Append("reportgenerator")
                .Append("--")
                .AppendQuoted($"-reports:{reportsArg}")
                .AppendQuoted($"-targetdir:{mergedReportDir.FullPath}")
                .Append("-reporttypes:TextSummary"),
        });

        if (reportGenExitCode != 0)
        {
            throw new CakeException($"reportgenerator failed with exit code {reportGenExitCode}.");
        }

        var summaryFile = mergedReportDir.CombineWithFilePath("Summary.txt");
        if (!context.FileExists(summaryFile))
        {
            throw new CakeException($"reportgenerator did not produce a summary at '{summaryFile}'.");
        }

        context.Information(string.Empty);
        context.Information(File.ReadAllText(summaryFile.FullPath));

        if (anyTestsFailed)
        {
            throw new CakeException("One or more test projects had failing tests -- see output above.");
        }
    }
}
```

- [ ] **Step 11: Create `build/Attributary.Build/Tasks/PackTask.cs`**

```csharp
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Pack")]
[IsDependentOn(typeof(BuildTask))]
public sealed class PackTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetPack("src/Attributary.Cli/Attributary.Cli.csproj", new DotNetPackSettings
        {
            Configuration = context.Configuration,
            NoRestore = true,
            OutputDirectory = "artifacts",
        });
    }
}
```

- [ ] **Step 12: Create `build/Attributary.Build/Tasks/DefaultTask.cs`**

```csharp
using Cake.Frosting;

namespace Attributary.Build.Tasks;

[TaskName("Default")]
[IsDependentOn(typeof(RestoreTask))]
[IsDependentOn(typeof(BuildTask))]
[IsDependentOn(typeof(FormatCheckTask))]
[IsDependentOn(typeof(AuditTask))]
[IsDependentOn(typeof(TestTask))]
public sealed class DefaultTask : FrostingTask<BuildContext>
{
}
```

- [ ] **Step 13: Create `build.ps1` at the repo root**

```powershell
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Target = "Default",

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & dotnet run --project build/Attributary.Build -- "--target=$Target" @RemainingArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
```

- [ ] **Step 14: Create `build.sh` at the repo root**

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

TARGET="${1:-Default}"
if [ "$#" -gt 0 ]; then
    shift
fi

dotnet run --project build/Attributary.Build -- "--target=$TARGET" "$@"
```

Run: `chmod +x build.sh` after creating it, so it's executable once checked out on Linux/macOS.

- [ ] **Step 15: Retire the old coverage script**

Run: `git rm scripts/Get-CodeCoverage.ps1`

If `scripts/` is now empty, remove it too (check with `git status` after the `rm` — an empty directory has nothing to remove from git, but delete it from disk so it doesn't linger).

- [ ] **Step 16: Verify the full default pipeline end-to-end**

Run: `./build.ps1` (Windows) or `./build.sh` (other platforms)

Expected: the very first console line is `Building Attributary version <X>` (from `BuildContext`'s `Cake.MinVer` call), then `Restore`, `Build`, `Format-Check`, `Audit`, and `Test` all run in that order and all succeed — the same outcome as running `dotnet restore`, `dotnet build`, `dotnet format --verify-no-changes`, `dotnet list package --vulnerable`, and the old `Get-CodeCoverage.ps1` script separately, but through one command. Coverage summary output should appear at the end, matching what `Get-CodeCoverage.ps1` used to print.

Cross-check the logged version against MSBuild's own computation: run `dotnet msbuild src/Attributary.Cli/Attributary.Cli.csproj -t:Build -getProperty:Version -nologo` (note: plain `-getProperty:Version` without `-t:Build` does not execute MinVer's target and falsely returns the SDK's static default `1.0.0` — always include `-t:Build` when checking a MinVer-derived version this way) and confirm it prints the exact same version string `./build.ps1` logged.

Run: `./build.ps1 Pack` (or `./build.sh Pack`)
Expected: succeeds, producing `artifacts/Attributary.<version>.nupkg` and `artifacts/Attributary.<version>.snupkg`.

Run: `./build.ps1 Clean` (or `./build.sh Clean`)
Expected: succeeds, removing `src/**/bin`, `src/**/obj`, `tests/**/bin`, `tests/**/obj`. Run `./build.ps1` again afterward to confirm the pipeline still works from a clean state.

- [ ] **Step 17: Commit**

```bash
git add Directory.Packages.props build/ build.ps1 build.sh
git rm scripts/Get-CodeCoverage.ps1
git commit -m "build: add Cake Frosting build orchestration with local/CI parity"
```

(If `scripts/` was deleted as an empty directory, `git rm` above already staged its removal — there's nothing else to add for it, since git doesn't track empty directories.)

---

## Self-Review Notes

- **Spec coverage:** §3.1 (Cake project) → Task 5. §3.2 (MinVer) → Task 2. §3.3 (CPM) → Task 1 (+ additions in Tasks 2/3/5). §3.4 (packaging metadata) → Task 3. §3.5 (code style) → Task 4 (`.editorconfig`) + Task 5 Step 7 (`TreatWarningsAsErrors` in the `Build` task, per the spec's "only for the build configuration the Cake `Build` task uses"). §3.6 (audit gate) → Task 2 Step 2 (SDK-level `NuGetAudit*` properties) + Task 5 Step 9 (`Audit` task). §3.7 (SDK pin) → Task 1 Step 2.
- **Vulnerability-detection logic** in Task 5's `AuditTask` was validated against this repo's actual `dotnet list package --vulnerable --include-transitive` output before writing the plan (confirmed phrase: `"has no vulnerable packages given the current sources."`, confirmed exit code `0` even when nothing is flagged) rather than assumed from documentation.
- **`MinVerTagPrefix` correction:** the spec originally stated MinVer defaults to a `v` prefix; this was factually wrong (MinVer's default prefix is empty) and has been corrected in both the spec and this plan — Task 2 explicitly sets `<MinVerTagPrefix>v</MinVerTagPrefix>`.
- **`Domain`/`Resolution`/`Artifacts`/`Output`/`Diagnostics` projects have no `PackageReference`s at all**, so Task 1 does not touch them — only `Cli`, `Rules`, `Sbom`, and the 7 test projects have package versions to strip.
- **Mid-execution amendment (added during Task 5 dispatch prep, after Tasks 1-2 were already committed and reviewed):** added `Cake.MinVer` 4.0.0 to Task 5 so the build itself logs the version it's building, using the same `TagPrefix="v"` convention as Task 2's `Directory.Build.props`. This does not change how packages/assemblies get versioned (still automatic via MSBuild) — it only adds visibility. Prompted by user feedback during the loop; folded into Task 5 before that task was dispatched, so no rework of already-completed tasks was needed.
- **`dotnet msbuild -getProperty:Version` verification-command defect (discovered during Task 2's review):** the plan originally told the implementer to verify MinVer via `dotnet msbuild <project> -getProperty:Version` with no `-t:Build`. That command does not execute MinVer's target and silently returns the SDK's static default (`1.0.0`) regardless of correctness. Confirmed by direct experiment (see Task 2's ledger entry). The underlying implementation was independently verified correct via `dotnet build -v:detailed` and via `dotnet msbuild <project> -t:Build -getProperty:Version`. Task 5 Step 16 has been updated to use the corrected `-t:Build` form when cross-checking the Cake-logged version against MSBuild's own computation.
