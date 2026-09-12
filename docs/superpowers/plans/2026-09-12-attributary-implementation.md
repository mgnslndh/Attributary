# Attributary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Attributary, a .NET 10 CLI tool that reads a CycloneDX SBOM and generates OSS compliance artifacts (per-license license texts, an aggregated NOTICE file, an attribution document, and a compliance report) driven by a configurable obligations rule engine.

**Architecture:** Seven independent class libraries, one per bounded context (Diagnostics, Domain, Sbom, Rules, Resolution, Artifacts, Output), wired together by a Spectre.Console.Cli composition root (Attributary.Cli). Data flows one direction: SBOM Ingestion → License Resolution → Rule Engine → Artifact Generation → Output Writers.

**Tech Stack:** .NET 10, C# (nullable enabled), TUnit + TUnit.Assertions for tests, Spectre.Console + Spectre.Console.Cli, official `CycloneDX` NuGet package, YamlDotNet, System.Text.Json, System.Security.Cryptography (SHA-256).

**Spec:** `docs/superpowers/specs/2026-09-12-attributary-design.md`

## Global Constraints

- Target framework `net10.0` everywhere; nullable reference types enabled solution-wide.
- Test projects use TUnit (`[Test]`, `await Assert.That(...)`), not xUnit/NUnit syntax.
- Diagnostic codes are ranged by stage: ATT0xxx config, ATT1xxx SBOM ingestion, ATT2xxx license resolution, ATT3xxx rule engine/policy, ATT4xxx artifact/output. Format: `{CODE} {severity}: {message} [{context}]`.
- Default severities: config/ingestion parse failures = Error; unresolved required data obligation = Error; policy `deny` = Error; policy `warn` = Warning; cache integrity mismatch = Warning.
- `defaults.unknownLicense.policy` in the rules DSL is `deny` unless a project's own config overrides it.
- No manual-overrides subsystem, no `.editorconfig` support, no automatic OR-expression resolution — these are explicit non-goals from the spec (§2).
- The CLI package is a .NET tool: `PackAsTool=true`, `ToolCommandName=attributary`, package id `Attributary`.

---

## Task 1: Solution scaffolding

**Files:**
- Create: `Attributary.sln`
- Create: `Directory.Build.props`
- Create: `global.json`
- Create: `src/Attributary.Diagnostics/Attributary.Diagnostics.csproj`
- Create: `src/Attributary.Domain/Attributary.Domain.csproj`
- Create: `src/Attributary.Sbom/Attributary.Sbom.csproj`
- Create: `src/Attributary.Rules/Attributary.Rules.csproj`
- Create: `src/Attributary.Resolution/Attributary.Resolution.csproj`
- Create: `src/Attributary.Artifacts/Attributary.Artifacts.csproj`
- Create: `src/Attributary.Output/Attributary.Output.csproj`
- Create: `src/Attributary.Cli/Attributary.Cli.csproj`
- Create: `tests/Attributary.Diagnostics.Tests/Attributary.Diagnostics.Tests.csproj`
- Create: `tests/Attributary.Sbom.Tests/Attributary.Sbom.Tests.csproj`
- Create: `tests/Attributary.Rules.Tests/Attributary.Rules.Tests.csproj`
- Create: `tests/Attributary.Resolution.Tests/Attributary.Resolution.Tests.csproj`
- Create: `tests/Attributary.Artifacts.Tests/Attributary.Artifacts.Tests.csproj`
- Create: `tests/Attributary.Output.Tests/Attributary.Output.Tests.csproj`
- Create: `tests/Attributary.Cli.Tests/Attributary.Cli.Tests.csproj`

**Interfaces:**
- Produces: project reference graph — `Rules`, `Sbom` reference `Domain`; `Rules` also references `Diagnostics`; `Resolution` references `Domain` + `Diagnostics`; `Artifacts` references `Domain` + `Rules`; `Output` references `Artifacts`; `Cli` references everything.

- [ ] **Step 1: Create the solution, `Directory.Build.props`, and `global.json`**

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

TUnit is built directly on Microsoft.Testing.Platform (MTP), not VSTest. As of .NET SDK 10, `dotnet test` gets native MTP support via a repo-root `global.json` setting — no per-project MSBuild property is needed (older guidance for .NET 9 previews used a `TestingPlatformDotnetTestSupport` csproj property; that property is now obsolete and must **not** be added, per the .NET 10 `dotnet test`/MTP migration guidance):

```json
// global.json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

```bash
dotnet new sln -n Attributary
```

- [ ] **Step 2: Create each library project (empty class libs) and each test project (TUnit)**

```bash
dotnet new classlib -o src/Attributary.Diagnostics
dotnet new classlib -o src/Attributary.Domain
dotnet new classlib -o src/Attributary.Sbom
dotnet new classlib -o src/Attributary.Rules
dotnet new classlib -o src/Attributary.Resolution
dotnet new classlib -o src/Attributary.Artifacts
dotnet new classlib -o src/Attributary.Output
dotnet new console -o src/Attributary.Cli

dotnet new install TUnit.Templates

dotnet new TUnit -o tests/Attributary.Diagnostics.Tests
dotnet new TUnit -o tests/Attributary.Sbom.Tests
dotnet new TUnit -o tests/Attributary.Rules.Tests
dotnet new TUnit -o tests/Attributary.Resolution.Tests
dotnet new TUnit -o tests/Attributary.Artifacts.Tests
dotnet new TUnit -o tests/Attributary.Output.Tests
dotnet new TUnit -o tests/Attributary.Cli.Tests
```

Delete the default `Class1.cs` / sample test file `dotnet new` generates in each project. Each generated test project targets `net8.0` by default and has `OutputType=Exe` with a bare `PackageReference Include="TUnit"` — do **not** add `Microsoft.NET.Test.Sdk` to these projects, it breaks MTP test discovery. `Directory.Build.props` (Step 1) overrides the target framework to `net10.0` for all projects including these.

- [ ] **Step 3: Wire project references and add all projects to the solution**

```bash
dotnet sln add src/**/*.csproj tests/**/*.csproj

dotnet add src/Attributary.Rules reference src/Attributary.Domain src/Attributary.Diagnostics
dotnet add src/Attributary.Sbom reference src/Attributary.Domain
dotnet add src/Attributary.Resolution reference src/Attributary.Domain src/Attributary.Diagnostics
dotnet add src/Attributary.Artifacts reference src/Attributary.Domain src/Attributary.Rules
dotnet add src/Attributary.Output reference src/Attributary.Artifacts
dotnet add src/Attributary.Cli reference src/Attributary.Diagnostics src/Attributary.Domain src/Attributary.Sbom src/Attributary.Rules src/Attributary.Resolution src/Attributary.Artifacts src/Attributary.Output

dotnet add tests/Attributary.Diagnostics.Tests reference src/Attributary.Diagnostics
dotnet add tests/Attributary.Sbom.Tests reference src/Attributary.Sbom
dotnet add tests/Attributary.Rules.Tests reference src/Attributary.Rules
dotnet add tests/Attributary.Resolution.Tests reference src/Attributary.Resolution
dotnet add tests/Attributary.Artifacts.Tests reference src/Attributary.Artifacts
dotnet add tests/Attributary.Output.Tests reference src/Attributary.Output
dotnet add tests/Attributary.Cli.Tests reference src/Attributary.Cli
```

- [ ] **Step 4: Verify the empty solution builds and tests run**

Run: `dotnet build Attributary.sln`
Expected: Build succeeds, 0 errors.

Run: `dotnet test Attributary.sln`
Expected: 0 tests found, 0 failures (each test project is currently empty).

- [ ] **Step 5: Commit**

```bash
git add Attributary.sln Directory.Build.props src tests
git commit -m "chore: scaffold solution and project structure"
```

---

## Task 2: Diagnostics — severity model and resolution

**Files:**
- Create: `src/Attributary.Diagnostics/DiagnosticSeverity.cs`
- Create: `src/Attributary.Diagnostics/DiagnosticDescriptor.cs`
- Create: `src/Attributary.Diagnostics/Diagnostic.cs`
- Create: `src/Attributary.Diagnostics/SeverityOverrides.cs`
- Create: `src/Attributary.Diagnostics/SeverityResolver.cs`
- Test: `tests/Attributary.Diagnostics.Tests/SeverityResolverTests.cs`

**Interfaces:**
- Produces:
  - `enum DiagnosticSeverity { Info, Warning, Error }`
  - `record DiagnosticDescriptor(string Code, DiagnosticSeverity DefaultSeverity, string Title)`
  - `record Diagnostic(DiagnosticDescriptor Descriptor, DiagnosticSeverity EffectiveSeverity, string Message, string? Context)`
  - `record SeverityOverrides(bool WarnAsErrorAll, IReadOnlySet<string> WarnAsErrorCodes, IReadOnlySet<string> WarnAsErrorExemptCodes, IReadOnlySet<string> NoWarnCodes, IReadOnlyDictionary<string, DiagnosticSeverity> ExplicitSeverities)` with a static `SeverityOverrides.None` value (all empty/false).
  - `static class SeverityResolver { static DiagnosticSeverity Resolve(DiagnosticDescriptor descriptor, SeverityOverrides overrides); }`

- [ ] **Step 1: Write the failing tests for precedence order**

```csharp
using Attributary.Diagnostics;

namespace Attributary.Diagnostics.Tests;

public class SeverityResolverTests
{
    private static readonly DiagnosticDescriptor WarningDescriptor =
        new("ATT2500", DiagnosticSeverity.Warning, "Cache integrity mismatch");

    private static readonly DiagnosticDescriptor ErrorDescriptor =
        new("ATT3001", DiagnosticSeverity.Error, "License policy deny");

    [Test]
    public async Task Resolve_NoOverrides_ReturnsDefaultSeverity()
    {
        var result = SeverityResolver.Resolve(WarningDescriptor, SeverityOverrides.None);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Resolve_WarnAsErrorAll_PromotesWarningToError()
    {
        var overrides = SeverityOverrides.None with { WarnAsErrorAll = true };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task Resolve_WarnAsErrorExempt_KeepsWarningEvenWithWarnAsErrorAll()
    {
        var overrides = SeverityOverrides.None with
        {
            WarnAsErrorAll = true,
            WarnAsErrorExemptCodes = new HashSet<string> { "ATT2500" }
        };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Resolve_NoWarn_SuppressesEvenWithWarnAsErrorAll()
    {
        var overrides = SeverityOverrides.None with
        {
            WarnAsErrorAll = true,
            NoWarnCodes = new HashSet<string> { "ATT2500" }
        };
        var result = SeverityResolver.Resolve(WarningDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Info);
    }

    [Test]
    public async Task Resolve_ExplicitSeverity_WinsOverEverythingElse()
    {
        var overrides = SeverityOverrides.None with
        {
            NoWarnCodes = new HashSet<string> { "ATT3001" },
            ExplicitSeverities = new Dictionary<string, DiagnosticSeverity> { ["ATT3001"] = DiagnosticSeverity.Warning }
        };
        var result = SeverityResolver.Resolve(ErrorDescriptor, overrides);
        await Assert.That(result).IsEqualTo(DiagnosticSeverity.Warning);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Diagnostics.Tests`
Expected: FAIL — `DiagnosticSeverity`, `DiagnosticDescriptor`, `SeverityOverrides`, `SeverityResolver` do not exist yet.

- [ ] **Step 3: Implement the types**

```csharp
// src/Attributary.Diagnostics/DiagnosticSeverity.cs
namespace Attributary.Diagnostics;

public enum DiagnosticSeverity { Info, Warning, Error }
```

```csharp
// src/Attributary.Diagnostics/DiagnosticDescriptor.cs
namespace Attributary.Diagnostics;

public sealed record DiagnosticDescriptor(string Code, DiagnosticSeverity DefaultSeverity, string Title);
```

```csharp
// src/Attributary.Diagnostics/Diagnostic.cs
namespace Attributary.Diagnostics;

public sealed record Diagnostic(
    DiagnosticDescriptor Descriptor,
    DiagnosticSeverity EffectiveSeverity,
    string Message,
    string? Context);
```

```csharp
// src/Attributary.Diagnostics/SeverityOverrides.cs
namespace Attributary.Diagnostics;

public sealed record SeverityOverrides(
    bool WarnAsErrorAll,
    IReadOnlySet<string> WarnAsErrorCodes,
    IReadOnlySet<string> WarnAsErrorExemptCodes,
    IReadOnlySet<string> NoWarnCodes,
    IReadOnlyDictionary<string, DiagnosticSeverity> ExplicitSeverities)
{
    public static readonly SeverityOverrides None = new(
        WarnAsErrorAll: false,
        WarnAsErrorCodes: new HashSet<string>(),
        WarnAsErrorExemptCodes: new HashSet<string>(),
        NoWarnCodes: new HashSet<string>(),
        ExplicitSeverities: new Dictionary<string, DiagnosticSeverity>());
}
```

```csharp
// src/Attributary.Diagnostics/SeverityResolver.cs
namespace Attributary.Diagnostics;

public static class SeverityResolver
{
    public static DiagnosticSeverity Resolve(DiagnosticDescriptor descriptor, SeverityOverrides overrides)
    {
        // Precedence: explicit > nowarn > warnaserror-exempt > warnaserror > default
        if (overrides.ExplicitSeverities.TryGetValue(descriptor.Code, out var explicitSeverity))
            return explicitSeverity;

        if (overrides.NoWarnCodes.Contains(descriptor.Code))
            return DiagnosticSeverity.Info;

        if (overrides.WarnAsErrorExemptCodes.Contains(descriptor.Code))
            return descriptor.DefaultSeverity;

        var promoteToError = overrides.WarnAsErrorAll || overrides.WarnAsErrorCodes.Contains(descriptor.Code);
        if (promoteToError && descriptor.DefaultSeverity == DiagnosticSeverity.Warning)
            return DiagnosticSeverity.Error;

        return descriptor.DefaultSeverity;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Diagnostics.Tests`
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Diagnostics tests/Attributary.Diagnostics.Tests
git commit -m "feat(diagnostics): add severity model and resolution precedence"
```

---

## Task 3: Diagnostics — sink and MSBuild-style formatter

**Files:**
- Create: `src/Attributary.Diagnostics/IDiagnosticSink.cs`
- Create: `src/Attributary.Diagnostics/DiagnosticSink.cs`
- Create: `src/Attributary.Diagnostics/IDiagnosticFormatter.cs`
- Create: `src/Attributary.Diagnostics/MsBuildStyleDiagnosticFormatter.cs`
- Test: `tests/Attributary.Diagnostics.Tests/DiagnosticSinkTests.cs`
- Test: `tests/Attributary.Diagnostics.Tests/MsBuildStyleDiagnosticFormatterTests.cs`

**Interfaces:**
- Consumes: `DiagnosticDescriptor`, `Diagnostic`, `SeverityOverrides`, `SeverityResolver` (Task 2).
- Produces:
  - `interface IDiagnosticSink { IReadOnlyList<Diagnostic> Diagnostics { get; } bool HasErrors { get; } void Report(DiagnosticDescriptor descriptor, string message, string? context = null); }`
  - `class DiagnosticSink(SeverityOverrides overrides) : IDiagnosticSink`
  - `interface IDiagnosticFormatter { string Format(Diagnostic diagnostic); }`
  - `class MsBuildStyleDiagnosticFormatter : IDiagnosticFormatter`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Diagnostics.Tests/DiagnosticSinkTests.cs
namespace Attributary.Diagnostics.Tests;

public class DiagnosticSinkTests
{
    [Test]
    public async Task Report_AddsDiagnosticWithResolvedSeverity()
    {
        var sink = new DiagnosticSink(SeverityOverrides.None);
        var descriptor = new DiagnosticDescriptor("ATT3001", DiagnosticSeverity.Error, "License policy deny");

        sink.Report(descriptor, "Component X is denied", "X 1.0.0");

        await Assert.That(sink.Diagnostics).HasCount().EqualTo(1);
        await Assert.That(sink.Diagnostics[0].EffectiveSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(sink.HasErrors).IsTrue();
    }

    [Test]
    public async Task HasErrors_FalseWhenOnlyWarnings()
    {
        var sink = new DiagnosticSink(SeverityOverrides.None);
        var descriptor = new DiagnosticDescriptor("ATT3002", DiagnosticSeverity.Warning, "License policy warn");

        sink.Report(descriptor, "Component Y needs review");

        await Assert.That(sink.HasErrors).IsFalse();
    }
}
```

```csharp
// tests/Attributary.Diagnostics.Tests/MsBuildStyleDiagnosticFormatterTests.cs
namespace Attributary.Diagnostics.Tests;

public class MsBuildStyleDiagnosticFormatterTests
{
    [Test]
    public async Task Format_WithContext_MatchesMsBuildStyle()
    {
        var descriptor = new DiagnosticDescriptor("ATT3001", DiagnosticSeverity.Error, "License policy deny");
        var diagnostic = new Diagnostic(descriptor, DiagnosticSeverity.Error, "License 'GPL-3.0-only' is denied", "Foo 1.2.3");
        var formatter = new MsBuildStyleDiagnosticFormatter();

        var result = formatter.Format(diagnostic);

        await Assert.That(result).IsEqualTo("ATT3001 error: License 'GPL-3.0-only' is denied [Foo 1.2.3]");
    }

    [Test]
    public async Task Format_WithoutContext_OmitsBrackets()
    {
        var descriptor = new DiagnosticDescriptor("ATT0002", DiagnosticSeverity.Error, "Unknown obligation");
        var diagnostic = new Diagnostic(descriptor, DiagnosticSeverity.Error, "Unknown obligation 'bogus'", null);
        var formatter = new MsBuildStyleDiagnosticFormatter();

        var result = formatter.Format(diagnostic);

        await Assert.That(result).IsEqualTo("ATT0002 error: Unknown obligation 'bogus'");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Diagnostics.Tests`
Expected: FAIL — `IDiagnosticSink`, `DiagnosticSink`, `IDiagnosticFormatter`, `MsBuildStyleDiagnosticFormatter` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Diagnostics/IDiagnosticSink.cs
namespace Attributary.Diagnostics;

public interface IDiagnosticSink
{
    IReadOnlyList<Diagnostic> Diagnostics { get; }
    bool HasErrors { get; }
    void Report(DiagnosticDescriptor descriptor, string message, string? context = null);
}
```

```csharp
// src/Attributary.Diagnostics/DiagnosticSink.cs
namespace Attributary.Diagnostics;

public sealed class DiagnosticSink(SeverityOverrides overrides) : IDiagnosticSink
{
    private readonly List<Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.EffectiveSeverity == DiagnosticSeverity.Error);

    public void Report(DiagnosticDescriptor descriptor, string message, string? context = null)
    {
        var severity = SeverityResolver.Resolve(descriptor, overrides);
        _diagnostics.Add(new Diagnostic(descriptor, severity, message, context));
    }
}
```

```csharp
// src/Attributary.Diagnostics/IDiagnosticFormatter.cs
namespace Attributary.Diagnostics;

public interface IDiagnosticFormatter
{
    string Format(Diagnostic diagnostic);
}
```

```csharp
// src/Attributary.Diagnostics/MsBuildStyleDiagnosticFormatter.cs
namespace Attributary.Diagnostics;

public sealed class MsBuildStyleDiagnosticFormatter : IDiagnosticFormatter
{
    public string Format(Diagnostic diagnostic)
    {
        var severityLabel = diagnostic.EffectiveSeverity.ToString().ToLowerInvariant();
        var suffix = diagnostic.Context is null ? "" : $" [{diagnostic.Context}]";
        return $"{diagnostic.Descriptor.Code} {severityLabel}: {diagnostic.Message}{suffix}";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Diagnostics.Tests`
Expected: PASS — all tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Diagnostics tests/Attributary.Diagnostics.Tests
git commit -m "feat(diagnostics): add diagnostic sink and MSBuild-style formatter"
```

---

## Task 4: Domain model and CycloneDX ingestion

**Files:**
- Create: `src/Attributary.Domain/LicenseExpression.cs`
- Create: `src/Attributary.Domain/ExternalReference.cs`
- Create: `src/Attributary.Domain/LicenseEvidence.cs`
- Create: `src/Attributary.Domain/SbomComponent.cs`
- Create: `src/Attributary.Sbom/CycloneDxIngestor.cs`
- Create: `src/Attributary.Sbom/Attributary.Sbom.csproj` (add `CycloneDX` package reference)
- Test: `tests/Attributary.Sbom.Tests/CycloneDxIngestorTests.cs`
- Test: `tests/Attributary.Sbom.Tests/fixtures/simple-mit.cdx.json`
- Test: `tests/Attributary.Sbom.Tests/fixtures/expression-and-evidence.cdx.json`

**Interfaces:**
- Produces:
  - `record LicenseExpression(string? SpdxId, string? FreeTextName, string? SpdxExpression)` with `static FromId(string)`, `static FromName(string)`, `static FromExpression(string)`, `bool IsSingleResolved`.
  - `enum ExternalReferenceType { Vcs, License, Website, Distribution, Other }`
  - `record ExternalReference(ExternalReferenceType Type, string Url)`
  - `record LicenseEvidence(string? SpdxId, string? Name, double? Confidence)`
  - `record SbomComponent(string Name, string Version, string? Purl, LicenseExpression DeclaredLicense, string? RawCopyright, IReadOnlyList<ExternalReference> ExternalReferences, IReadOnlyList<LicenseEvidence> Evidence)`
  - `class CycloneDxIngestor { IReadOnlyList<SbomComponent> Ingest(string filePath); }`

- [ ] **Step 1: Add the CycloneDX package reference and fixture files**

```bash
dotnet add src/Attributary.Sbom package CycloneDX.Core
```

```json
// tests/Attributary.Sbom.Tests/fixtures/simple-mit.cdx.json
{
  "bomFormat": "CycloneDX",
  "specVersion": "1.5",
  "version": 1,
  "components": [
    {
      "type": "library",
      "name": "Newtonsoft.Json",
      "version": "13.0.3",
      "purl": "pkg:nuget/Newtonsoft.Json@13.0.3",
      "copyright": "Copyright (c) 2007 James Newton-King",
      "licenses": [ { "license": { "id": "MIT" } } ]
    }
  ]
}
```

```json
// tests/Attributary.Sbom.Tests/fixtures/expression-and-evidence.cdx.json
{
  "bomFormat": "CycloneDX",
  "specVersion": "1.5",
  "version": 1,
  "components": [
    {
      "type": "library",
      "name": "dual-licensed-lib",
      "version": "2.0.0",
      "licenses": [ { "expression": "(MIT OR Apache-2.0)" } ],
      "externalReferences": [
        { "type": "vcs", "url": "https://github.com/example/dual-licensed-lib" }
      ],
      "evidence": {
        "licenses": [ { "license": { "id": "MIT" } } ]
      }
    }
  ]
}
```

Mark both fixture files "Copy to Output Directory" in the test csproj:

```xml
<ItemGroup>
  <None Include="fixtures\**\*.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Attributary.Sbom.Tests/CycloneDxIngestorTests.cs
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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Sbom.Tests`
Expected: FAIL — `Attributary.Domain` types and `CycloneDxIngestor` do not exist.

- [ ] **Step 4: Implement the domain types and the ingestor**

```csharp
// src/Attributary.Domain/LicenseExpression.cs
namespace Attributary.Domain;

public sealed record LicenseExpression(string? SpdxId, string? FreeTextName, string? SpdxExpression)
{
    public static LicenseExpression FromId(string id) => new(id, null, null);
    public static LicenseExpression FromName(string name) => new(null, name, null);
    public static LicenseExpression FromExpression(string expression) => new(null, null, expression);

    public bool IsSingleResolved => SpdxId is not null || FreeTextName is not null;
}
```

```csharp
// src/Attributary.Domain/ExternalReference.cs
namespace Attributary.Domain;

public enum ExternalReferenceType { Vcs, License, Website, Distribution, Other }

public sealed record ExternalReference(ExternalReferenceType Type, string Url);
```

```csharp
// src/Attributary.Domain/LicenseEvidence.cs
namespace Attributary.Domain;

public sealed record LicenseEvidence(string? SpdxId, string? Name, double? Confidence);
```

```csharp
// src/Attributary.Domain/SbomComponent.cs
namespace Attributary.Domain;

public sealed record SbomComponent(
    string Name,
    string Version,
    string? Purl,
    LicenseExpression DeclaredLicense,
    string? RawCopyright,
    IReadOnlyList<ExternalReference> ExternalReferences,
    IReadOnlyList<LicenseEvidence> Evidence);
```

```csharp
// src/Attributary.Sbom/CycloneDxIngestor.cs
using Attributary.Domain;
using CycloneDX.Json;
using CycloneDX.Models;

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
            .Select(r => new ExternalReference(MapReferenceType(r.Type), r.Url))
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

        var first = licenses[0];
        if (first.Expression is not null)
            return LicenseExpression.FromExpression(first.Expression);

        if (first.License?.Id is not null)
            return LicenseExpression.FromId(first.License.Id);

        return LicenseExpression.FromName(first.License?.Name ?? "UNKNOWN");
    }

    private static ExternalReferenceType MapReferenceType(ExternalReference.ExternalReferenceType type) => type switch
    {
        ExternalReference.ExternalReferenceType.Vcs => ExternalReferenceType.Vcs,
        ExternalReference.ExternalReferenceType.License => ExternalReferenceType.License,
        ExternalReference.ExternalReferenceType.Website => ExternalReferenceType.Website,
        ExternalReference.ExternalReferenceType.Distribution => ExternalReferenceType.Distribution,
        _ => ExternalReferenceType.Other
    };
}
```

Verified against the `CycloneDX.Core` source (github.com/CycloneDX/cyclonedx-dotnet-library): `Bom.Components` is `List<Component>`; `Component.Licenses` is `List<LicenseChoice>` where each `LicenseChoice` has either `.License` (with `.Id`/`.Name`) or `.Expression`; `Component.ExternalReferences` is `List<ExternalReference>` with a nested `ExternalReference.ExternalReferenceType` enum (`Vcs`, `Website`, `Distribution`, `License`, ...); `Component.Evidence.Licenses` is also `List<LicenseChoice>`. If a newer package version has renamed something, the test fixtures and expected `SbomComponent` shape don't change — only `MapComponent`/`MapDeclaredLicense`/`MapReferenceType` would need adjusting.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Sbom.Tests`
Expected: PASS — both tests green.

- [ ] **Step 6: Commit**

```bash
git add src/Attributary.Domain src/Attributary.Sbom tests/Attributary.Sbom.Tests
git commit -m "feat(sbom): add domain model and CycloneDX ingestion"
```

---

## Task 5: Rule engine — obligations model and YAML rule set loading

**Files:**
- Create: `src/Attributary.Rules/Obligation.cs`
- Create: `src/Attributary.Rules/ObligationFlag.cs`
- Create: `src/Attributary.Rules/LicensePolicy.cs`
- Create: `src/Attributary.Rules/LicenseRule.cs`
- Create: `src/Attributary.Rules/RuleSet.cs`
- Create: `src/Attributary.Rules/IRuleSetLoader.cs`
- Create: `src/Attributary.Rules/YamlRuleSetLoader.cs`
- Test: `tests/Attributary.Rules.Tests/YamlRuleSetLoaderTests.cs`

**Interfaces:**
- Produces:
  - `enum ObligationKind { Copyright, LicenseText, NoticeText }`
  - `record Obligation(ObligationKind Kind, string? Condition)`
  - `enum ObligationFlag { SourceOffer, ModificationDisclosure, NonEndorsement, TrademarkNonGrant, PatentGrant, AdvertisingClause, NonOsiApproved, CopyleftWeak, CopyleftStrong }`
  - `enum LicensePolicy { Allow, Warn, Deny }`
  - `record LicenseRule(string IdPattern, LicensePolicy Policy, IReadOnlyList<Obligation> Require, IReadOnlyList<ObligationFlag> Flags)`
  - `record RuleSet(LicenseRule UnknownLicenseDefault, IReadOnlyList<LicenseRule> Rules)`
  - `interface IRuleSetLoader { RuleSet Load(string yaml); }`
  - `class YamlRuleSetLoader : IRuleSetLoader`

- [ ] **Step 1: Add YamlDotNet and write the failing test**

```bash
dotnet add src/Attributary.Rules package YamlDotNet
```

```csharp
// tests/Attributary.Rules.Tests/YamlRuleSetLoaderTests.cs
namespace Attributary.Rules.Tests;

public class YamlRuleSetLoaderTests
{
    private const string Yaml = """
        defaults:
          unknownLicense:
            policy: deny
            require: [copyright, license-text]

        rules:
          - id: MIT
            policy: allow
            require: [copyright, license-text]

          - id: Apache-2.0
            policy: allow
            require:
              - copyright
              - license-text
              - notice-text: { when: upstream-notice-present }
            flags: [modification-disclosure, trademark-non-grant, patent-grant]

          - id: BSD-3-Clause
            policy: allow
            require: [copyright, license-text]
            flags: [non-endorsement]
        """;

    [Test]
    public async Task Load_UnknownLicenseDefault_IsDenyWithBaselineObligations()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        await Assert.That(ruleSet.UnknownLicenseDefault.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(ruleSet.UnknownLicenseDefault.Require).HasCount().EqualTo(2);
        await Assert.That(ruleSet.UnknownLicenseDefault.Require.Select(r => r.Kind))
            .Contains(ObligationKind.Copyright).And.Contains(ObligationKind.LicenseText);
    }

    [Test]
    public async Task Load_ApacheRule_HasConditionalNoticeObligationAndFlags()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        var apache = ruleSet.Rules.Single(r => r.IdPattern == "Apache-2.0");
        var noticeObligation = apache.Require.Single(r => r.Kind == ObligationKind.NoticeText);

        await Assert.That(apache.Policy).IsEqualTo(LicensePolicy.Allow);
        await Assert.That(noticeObligation.Condition).IsEqualTo("upstream-notice-present");
        await Assert.That(apache.Flags).Contains(ObligationFlag.ModificationDisclosure)
            .And.Contains(ObligationFlag.TrademarkNonGrant)
            .And.Contains(ObligationFlag.PatentGrant);
    }

    [Test]
    public async Task Load_Bsd3Rule_HasNonEndorsementFlagAndNoCondition()
    {
        var ruleSet = new YamlRuleSetLoader().Load(Yaml);

        var bsd3 = ruleSet.Rules.Single(r => r.IdPattern == "BSD-3-Clause");

        await Assert.That(bsd3.Flags).Contains(ObligationFlag.NonEndorsement);
        await Assert.That(bsd3.Require.All(r => r.Condition is null)).IsTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: FAIL — none of the rule engine types exist yet.

- [ ] **Step 3: Implement the model types and loader**

```csharp
// src/Attributary.Rules/Obligation.cs
namespace Attributary.Rules;

public enum ObligationKind { Copyright, LicenseText, NoticeText }

public sealed record Obligation(ObligationKind Kind, string? Condition);
```

```csharp
// src/Attributary.Rules/ObligationFlag.cs
namespace Attributary.Rules;

public enum ObligationFlag
{
    SourceOffer, ModificationDisclosure, NonEndorsement, TrademarkNonGrant,
    PatentGrant, AdvertisingClause, NonOsiApproved, CopyleftWeak, CopyleftStrong
}
```

```csharp
// src/Attributary.Rules/LicensePolicy.cs
namespace Attributary.Rules;

public enum LicensePolicy { Allow, Warn, Deny }
```

```csharp
// src/Attributary.Rules/LicenseRule.cs
namespace Attributary.Rules;

public sealed record LicenseRule(
    string IdPattern,
    LicensePolicy Policy,
    IReadOnlyList<Obligation> Require,
    IReadOnlyList<ObligationFlag> Flags);
```

```csharp
// src/Attributary.Rules/RuleSet.cs
namespace Attributary.Rules;

public sealed record RuleSet(LicenseRule UnknownLicenseDefault, IReadOnlyList<LicenseRule> Rules);
```

```csharp
// src/Attributary.Rules/IRuleSetLoader.cs
namespace Attributary.Rules;

public interface IRuleSetLoader
{
    RuleSet Load(string yamlContent);
}
```

```csharp
// src/Attributary.Rules/YamlRuleSetLoader.cs
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Attributary.Rules;

public sealed class YamlRuleSetLoader : IRuleSetLoader
{
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public RuleSet Load(string yamlContent)
    {
        var raw = _deserializer.Deserialize<RawRulesFile>(yamlContent);

        var unknownDefault = MapRule("*", raw.Defaults.UnknownLicense.Policy, raw.Defaults.UnknownLicense.Require, []);
        var rules = raw.Rules.Select(r => MapRule(r.Id, r.Policy, r.Require, r.Flags ?? [])).ToList();

        return new RuleSet(unknownDefault, rules);
    }

    private static LicenseRule MapRule(string idPattern, string policy, List<object> rawRequire, List<string> rawFlags)
    {
        var require = rawRequire.Select(MapObligation).ToList();
        var flags = rawFlags.Select(MapFlag).ToList();
        return new LicenseRule(idPattern, MapPolicy(policy), require, flags);
    }

    private static Obligation MapObligation(object raw)
    {
        if (raw is string simple)
            return new Obligation(MapObligationKind(simple), null);

        // notice-text: { when: upstream-notice-present } deserializes as a
        // single-entry Dictionary<object, object> from YamlDotNet.
        var map = (Dictionary<object, object>)raw;
        var kindName = (string)map.Keys.Single();
        var conditionMap = (Dictionary<object, object>)map.Values.Single();
        var condition = (string)conditionMap["when"];
        return new Obligation(MapObligationKind(kindName), condition);
    }

    private static ObligationKind MapObligationKind(string name) => name switch
    {
        "copyright" => ObligationKind.Copyright,
        "license-text" => ObligationKind.LicenseText,
        "notice-text" => ObligationKind.NoticeText,
        _ => throw new InvalidOperationException($"Unknown obligation '{name}'")
    };

    private static ObligationFlag MapFlag(string name) => name switch
    {
        "source-offer" => ObligationFlag.SourceOffer,
        "modification-disclosure" => ObligationFlag.ModificationDisclosure,
        "non-endorsement" => ObligationFlag.NonEndorsement,
        "trademark-non-grant" => ObligationFlag.TrademarkNonGrant,
        "patent-grant" => ObligationFlag.PatentGrant,
        "advertising-clause" => ObligationFlag.AdvertisingClause,
        "non-osi-approved" => ObligationFlag.NonOsiApproved,
        "copyleft-weak" => ObligationFlag.CopyleftWeak,
        "copyleft-strong" => ObligationFlag.CopyleftStrong,
        _ => throw new InvalidOperationException($"Unknown flag '{name}'")
    };

    private static LicensePolicy MapPolicy(string name) => name switch
    {
        "allow" => LicensePolicy.Allow,
        "warn" => LicensePolicy.Warn,
        "deny" => LicensePolicy.Deny,
        _ => throw new InvalidOperationException($"Unknown policy '{name}'")
    };

    private sealed class RawRulesFile
    {
        public RawDefaults Defaults { get; set; } = new();
        public List<RawRule> Rules { get; set; } = [];
    }

    private sealed class RawDefaults
    {
        public RawRule UnknownLicense { get; set; } = new();
    }

    private sealed class RawRule
    {
        public string Id { get; set; } = "";
        public string Policy { get; set; } = "deny";
        public List<object> Require { get; set; } = [];
        public List<string>? Flags { get; set; }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Rules tests/Attributary.Rules.Tests
git commit -m "feat(rules): add obligations model and YAML rule set loading"
```

---

## Task 6: Rule engine — matching, AND-composition, and obligation plans

**Files:**
- Create: `src/Attributary.Rules/IRuleMatcher.cs`
- Create: `src/Attributary.Rules/RuleMatcher.cs`
- Create: `src/Attributary.Rules/ObligationPlan.cs`
- Create: `src/Attributary.Rules/IObligationPlanBuilder.cs`
- Create: `src/Attributary.Rules/ObligationPlanBuilder.cs`
- Test: `tests/Attributary.Rules.Tests/RuleMatcherTests.cs`
- Test: `tests/Attributary.Rules.Tests/ObligationPlanBuilderTests.cs`

**Interfaces:**
- Consumes: `LicenseRule`, `RuleSet`, `Obligation`, `ObligationFlag`, `LicensePolicy` (Task 5); `LicenseResolution` (Task 4's domain project — added in this task, see Step 3).
- Produces:
  - `interface IRuleMatcher { LicenseRule Match(string licenseId, RuleSet ruleSet); LicenseRule MatchExpression(IReadOnlyList<string> licenseIds, RuleSet ruleSet); }`
  - `class RuleMatcher : IRuleMatcher`
  - `record ObligationPlan(Attributary.Domain.LicenseResolution Resolution, LicensePolicy Policy, IReadOnlyList<Obligation> Obligations, IReadOnlyList<ObligationFlag> Flags)`
  - `interface IObligationPlanBuilder { ObligationPlan Build(Attributary.Domain.LicenseResolution resolution, RuleSet ruleSet, IReadOnlyDictionary<string, bool> conditionResults); }`
  - `class ObligationPlanBuilder(IRuleMatcher matcher) : IObligationPlanBuilder`
  - New domain type (add to `Attributary.Domain`): `record LicenseResolution(SbomComponent Component, string? ResolvedLicenseId, string? CopyrightText, string? LicenseText, string? NoticeText)`.

- [ ] **Step 1: Write the failing tests for rule matching (exact, glob, default, AND-composition)**

```csharp
// tests/Attributary.Rules.Tests/RuleMatcherTests.cs
namespace Attributary.Rules.Tests;

public class RuleMatcherTests
{
    private static RuleSet BuildRuleSet() => new(
        UnknownLicenseDefault: new LicenseRule("*", LicensePolicy.Deny,
            [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
        Rules:
        [
            new LicenseRule("MIT", LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
            new LicenseRule("GPL-3.0-*", LicensePolicy.Warn,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [ObligationFlag.SourceOffer, ObligationFlag.CopyleftStrong])
        ]);

    [Test]
    public async Task Match_ExactId_ReturnsMatchingRule()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("MIT", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Allow);
    }

    [Test]
    public async Task Match_GlobPattern_MatchesVersionFamily()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("GPL-3.0-only", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(rule.Flags).Contains(ObligationFlag.CopyleftStrong);
    }

    [Test]
    public async Task Match_NoRuleFound_ReturnsUnknownDefault()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.Match("Some-Obscure-License", BuildRuleSet());
        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Deny);
    }

    [Test]
    public async Task MatchExpression_TwoLicenses_UnionsObligationsAndTakesMostRestrictivePolicy()
    {
        var matcher = new RuleMatcher();
        var rule = matcher.MatchExpression(["MIT", "GPL-3.0-only"], BuildRuleSet());

        await Assert.That(rule.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(rule.Flags).Contains(ObligationFlag.SourceOffer);
        await Assert.That(rule.Require.Select(r => r.Kind).Distinct()).HasCount().EqualTo(2);
    }
}
```

```csharp
// tests/Attributary.Rules.Tests/ObligationPlanBuilderTests.cs
using Attributary.Domain;

namespace Attributary.Rules.Tests;

public class ObligationPlanBuilderTests
{
    private static SbomComponent BuildComponent(string licenseId) => new(
        Name: "Foo", Version: "1.0.0", Purl: null,
        DeclaredLicense: LicenseExpression.FromId(licenseId),
        RawCopyright: "Copyright Foo Inc.",
        ExternalReferences: [], Evidence: []);

    private static RuleSet BuildRuleSet() => new(
        UnknownLicenseDefault: new LicenseRule("*", LicensePolicy.Deny,
            [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)], []),
        Rules:
        [
            new LicenseRule("Apache-2.0", LicensePolicy.Allow,
                [
                    new Obligation(ObligationKind.Copyright, null),
                    new Obligation(ObligationKind.LicenseText, null),
                    new Obligation(ObligationKind.NoticeText, "upstream-notice-present")
                ],
                [ObligationFlag.ModificationDisclosure])
        ]);

    [Test]
    public async Task Build_ConditionResolvesTrue_KeepsConditionalObligation()
    {
        var resolution = new LicenseResolution(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", "Notice text");
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = true });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).Contains(ObligationKind.NoticeText);
        await Assert.That(plan.Policy).IsEqualTo(LicensePolicy.Allow);
    }

    [Test]
    public async Task Build_ConditionResolvesFalse_DropsConditionalObligationSilently()
    {
        var resolution = new LicenseResolution(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", null);
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = false });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).DoesNotContain(ObligationKind.NoticeText);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: FAIL — `RuleMatcher`, `ObligationPlanBuilder`, `LicenseResolution` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Domain/LicenseResolution.cs (new file in Attributary.Domain)
namespace Attributary.Domain;

public sealed record LicenseResolution(
    SbomComponent Component,
    string? ResolvedLicenseId,
    string? CopyrightText,
    string? LicenseText,
    string? NoticeText);
```

```csharp
// src/Attributary.Rules/IRuleMatcher.cs
namespace Attributary.Rules;

public interface IRuleMatcher
{
    LicenseRule Match(string licenseId, RuleSet ruleSet);
    LicenseRule MatchExpression(IReadOnlyList<string> licenseIds, RuleSet ruleSet);
}
```

```csharp
// src/Attributary.Rules/RuleMatcher.cs
using System.Text.RegularExpressions;

namespace Attributary.Rules;

public sealed class RuleMatcher : IRuleMatcher
{
    public LicenseRule Match(string licenseId, RuleSet ruleSet)
    {
        var exact = ruleSet.Rules.FirstOrDefault(r => r.IdPattern == licenseId);
        if (exact is not null)
            return exact;

        var glob = ruleSet.Rules.FirstOrDefault(r => r.IdPattern.EndsWith('*')
            && licenseId.StartsWith(r.IdPattern[..^1], StringComparison.Ordinal));
        if (glob is not null)
            return glob;

        return ruleSet.UnknownLicenseDefault;
    }

    public LicenseRule MatchExpression(IReadOnlyList<string> licenseIds, RuleSet ruleSet)
    {
        var matched = licenseIds.Select(id => Match(id, ruleSet)).ToList();

        var mostRestrictivePolicy = matched.Select(r => r.Policy).Max();
        var unionRequire = matched.SelectMany(r => r.Require).DistinctBy(o => (o.Kind, o.Condition)).ToList();
        var unionFlags = matched.SelectMany(r => r.Flags).Distinct().ToList();

        return new LicenseRule(string.Join(" AND ", licenseIds), mostRestrictivePolicy, unionRequire, unionFlags);
    }
}
```

`LicensePolicy` must be declared `Allow = 0, Warn = 1, Deny = 2` (already the declaration order from Task 5) so `Enum.Max` picks the most restrictive value correctly.

```csharp
// src/Attributary.Rules/ObligationPlan.cs
namespace Attributary.Rules;

public sealed record ObligationPlan(
    Attributary.Domain.LicenseResolution Resolution,
    LicensePolicy Policy,
    IReadOnlyList<Obligation> Obligations,
    IReadOnlyList<ObligationFlag> Flags);
```

```csharp
// src/Attributary.Rules/IObligationPlanBuilder.cs
namespace Attributary.Rules;

public interface IObligationPlanBuilder
{
    ObligationPlan Build(
        Attributary.Domain.LicenseResolution resolution,
        RuleSet ruleSet,
        IReadOnlyDictionary<string, bool> conditionResults);
}
```

```csharp
// src/Attributary.Rules/ObligationPlanBuilder.cs
namespace Attributary.Rules;

public sealed class ObligationPlanBuilder(IRuleMatcher matcher) : IObligationPlanBuilder
{
    public ObligationPlan Build(
        Attributary.Domain.LicenseResolution resolution,
        RuleSet ruleSet,
        IReadOnlyDictionary<string, bool> conditionResults)
    {
        var licenseId = resolution.ResolvedLicenseId ?? "UNKNOWN";
        var rule = matcher.Match(licenseId, ruleSet);

        var obligations = rule.Require
            .Where(o => o.Condition is null || conditionResults.GetValueOrDefault(o.Condition, false))
            .ToList();

        return new ObligationPlan(resolution, rule.Policy, obligations, rule.Flags);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: PASS — all tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Domain src/Attributary.Rules tests/Attributary.Rules.Tests
git commit -m "feat(rules): add rule matching, AND-composition, and obligation plan builder"
```

---

## Task 7: Bundled default rule set

**Files:**
- Create: `src/Attributary.Rules/DefaultRules.yaml` (embedded resource)
- Modify: `src/Attributary.Rules/Attributary.Rules.csproj` (embed the resource)
- Create: `src/Attributary.Rules/IDefaultRuleSetProvider.cs`
- Create: `src/Attributary.Rules/DefaultRuleSetProvider.cs`
- Test: `tests/Attributary.Rules.Tests/DefaultRuleSetProviderTests.cs`

**Interfaces:**
- Consumes: `IRuleSetLoader`, `RuleSet`, `LicensePolicy`, `ObligationFlag` (Tasks 5-6).
- Produces: `interface IDefaultRuleSetProvider { RuleSet Load(); }`, `class DefaultRuleSetProvider(IRuleSetLoader loader) : IDefaultRuleSetProvider`.

- [ ] **Step 1: Add the embedded resource to the csproj**

```xml
<!-- add inside src/Attributary.Rules/Attributary.Rules.csproj's existing <Project> -->
<ItemGroup>
  <EmbeddedResource Include="DefaultRules.yaml" />
</ItemGroup>
```

- [ ] **Step 2: Write `DefaultRules.yaml`, covering the license families from the design research**

```yaml
# src/Attributary.Rules/DefaultRules.yaml
defaults:
  unknownLicense:
    policy: deny
    require: [copyright, license-text]

rules:
  - id: MIT
    policy: allow
    require: [copyright, license-text]

  - id: ISC
    policy: allow
    require: [copyright, license-text]

  - id: BSD-2-Clause
    policy: allow
    require: [copyright, license-text]

  - id: BSD-3-Clause
    policy: allow
    require: [copyright, license-text]
    flags: [non-endorsement]

  - id: BSD-4-Clause
    policy: warn
    require: [copyright, license-text]
    flags: [advertising-clause]

  - id: Apache-2.0
    policy: allow
    require:
      - copyright
      - license-text
      - notice-text: { when: upstream-notice-present }
    flags: [modification-disclosure, trademark-non-grant, patent-grant]

  - id: MPL-2.0
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, modification-disclosure, copyleft-weak]

  - id: LGPL-2.1-*
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-weak]

  - id: LGPL-3.0-*
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-weak]

  - id: GPL-2.0-*
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-strong]

  - id: GPL-3.0-*
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-strong]

  - id: AGPL-3.0-*
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-strong]

  - id: CDDL-1.0
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-weak]

  - id: CDDL-1.1
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-weak]

  - id: EPL-1.0
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, patent-grant]

  - id: EPL-2.0
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, patent-grant]

  - id: Python-2.0
    policy: allow
    require: [copyright, license-text]

  - id: Zlib
    policy: allow
    require: [copyright, license-text]

  - id: BSL-1.0
    policy: allow
    require: [copyright, license-text]

  - id: Unlicense
    policy: allow
    require: [license-text]

  - id: CC0-1.0
    policy: allow
    require: [license-text]

  - id: WTFPL
    policy: allow
    require: [license-text]

  - id: JSON
    policy: deny
    require: [copyright, license-text]
    flags: [non-osi-approved]
```

- [ ] **Step 3: Write the failing test**

```csharp
// tests/Attributary.Rules.Tests/DefaultRuleSetProviderTests.cs
namespace Attributary.Rules.Tests;

public class DefaultRuleSetProviderTests
{
    [Test]
    public async Task Load_ReturnsAllBundledLicenses()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();

        await Assert.That(ruleSet.UnknownLicenseDefault.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(ruleSet.Rules.Select(r => r.IdPattern)).Contains("MIT").And.Contains("Apache-2.0");
    }

    [Test]
    public async Task Load_JsonLicense_IsDeniedAndFlaggedNonOsiApproved()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();
        var json = ruleSet.Rules.Single(r => r.IdPattern == "JSON");

        await Assert.That(json.Policy).IsEqualTo(LicensePolicy.Deny);
        await Assert.That(json.Flags).Contains(ObligationFlag.NonOsiApproved);
    }

    [Test]
    public async Task Load_Gpl3Family_IsWarnWithSourceOfferAndCopyleftStrong()
    {
        var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

        var ruleSet = provider.Load();
        var gpl3 = ruleSet.Rules.Single(r => r.IdPattern == "GPL-3.0-*");

        await Assert.That(gpl3.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(gpl3.Flags).Contains(ObligationFlag.SourceOffer).And.Contains(ObligationFlag.CopyleftStrong);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail, then implement**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: FAIL — `IDefaultRuleSetProvider`/`DefaultRuleSetProvider` do not exist.

```csharp
// src/Attributary.Rules/IDefaultRuleSetProvider.cs
namespace Attributary.Rules;

public interface IDefaultRuleSetProvider
{
    RuleSet Load();
}
```

```csharp
// src/Attributary.Rules/DefaultRuleSetProvider.cs
using System.Reflection;

namespace Attributary.Rules;

public sealed class DefaultRuleSetProvider(IRuleSetLoader loader) : IDefaultRuleSetProvider
{
    public RuleSet Load()
    {
        var assembly = typeof(DefaultRuleSetProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("DefaultRules.yaml"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return loader.Load(reader.ReadToEnd());
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/Attributary.Rules tests/Attributary.Rules.Tests
git commit -m "feat(rules): add bundled default rule set covering common OSS licenses"
```

---

## Task 8: License resolution — provenance model and local (offline) sources

**Files:**
- Modify: `src/Attributary.Domain/LicenseResolution.cs` (add `Provenance` property)
- Create: `src/Attributary.Domain/ResolutionProvenance.cs`
- Create: `src/Attributary.Resolution/SourceResult.cs`
- Create: `src/Attributary.Resolution/ILicenseSource.cs`
- Create: `src/Attributary.Resolution/Sources/SbomEmbeddedSource.cs`
- Create: `src/Attributary.Resolution/Sources/SpdxCanonicalSource.cs`
- Create: `src/Attributary.Resolution/SpdxLicenseData/spdx-subset.json` (embedded resource)
- Modify: `src/Attributary.Resolution/Attributary.Resolution.csproj` (embed the resource)
- Test: `tests/Attributary.Resolution.Tests/SbomEmbeddedSourceTests.cs`
- Test: `tests/Attributary.Resolution.Tests/SpdxCanonicalSourceTests.cs`

**Interfaces:**
- Consumes: `SbomComponent`, `LicenseExpression` (Task 4).
- Produces:
  - `enum ResolutionSourceStrategy { SbomEmbedded, LocalPackageCache, VcsRepository, SpdxCanonical }`
  - `record FieldProvenance(ResolutionSourceStrategy Strategy, string? SourceUrl, DateTimeOffset ResolvedAtUtc, bool FromCache)`
  - `record ResolutionProvenance(FieldProvenance? LicenseTextProvenance, FieldProvenance? CopyrightProvenance, FieldProvenance? NoticeTextProvenance)`
  - `record LicenseResolution(..., ResolutionProvenance? Provenance = null)` — existing record from Task 6, extended with a 6th optional parameter so Task 6's call sites keep compiling.
  - `record SourceResult(bool Resolved, string? LicenseText, string? CopyrightText, string? NoticeText, FieldProvenance? Provenance)`
  - `interface ILicenseSource { ResolutionSourceStrategy Strategy { get; } Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct); }`
  - `class SbomEmbeddedSource : ILicenseSource`
  - `class SpdxCanonicalSource : ILicenseSource`

- [ ] **Step 1: Extend the domain model**

```csharp
// src/Attributary.Domain/ResolutionProvenance.cs
namespace Attributary.Domain;

public enum ResolutionSourceStrategy { SbomEmbedded, LocalPackageCache, VcsRepository, SpdxCanonical }

public sealed record FieldProvenance(ResolutionSourceStrategy Strategy, string? SourceUrl, DateTimeOffset ResolvedAtUtc, bool FromCache);

public sealed record ResolutionProvenance(
    FieldProvenance? LicenseTextProvenance,
    FieldProvenance? CopyrightProvenance,
    FieldProvenance? NoticeTextProvenance)
{
    public static readonly ResolutionProvenance Empty = new(null, null, null);
}
```

```csharp
// src/Attributary.Domain/LicenseResolution.cs — modify the existing record from Task 6
namespace Attributary.Domain;

public sealed record LicenseResolution(
    SbomComponent Component,
    string? ResolvedLicenseId,
    string? CopyrightText,
    string? LicenseText,
    string? NoticeText,
    ResolutionProvenance? Provenance = null);
```

- [ ] **Step 2: Add a small SPDX text subset as an embedded resource and wire it into the csproj**

```xml
<!-- add inside src/Attributary.Resolution/Attributary.Resolution.csproj's existing <Project> -->
<ItemGroup>
  <EmbeddedResource Include="SpdxLicenseData\spdx-subset.json" />
</ItemGroup>
```

```json
// src/Attributary.Resolution/SpdxLicenseData/spdx-subset.json
{
  "MIT": "MIT License\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction...",
  "Apache-2.0": "Apache License\nVersion 2.0, January 2004\nhttp://www.apache.org/licenses/\n\nTERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION...",
  "BSD-3-Clause": "BSD 3-Clause License\n\nRedistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met..."
}
```

This is a starter subset (3 entries) sufficient for the fixtures in this task and Task 9; expanding it to the full `spdx/license-list-data` corpus is a mechanical follow-up (vendor the upstream JSON files wholesale) that doesn't change any type or test shape here.

- [ ] **Step 3: Write the failing tests**

```csharp
// tests/Attributary.Resolution.Tests/SbomEmbeddedSourceTests.cs
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

public class SbomEmbeddedSourceTests
{
    [Test]
    public async Task TryResolveAsync_CopyrightPresentOnComponent_ResolvesCopyright()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: "Copyright (c) 2020 Foo Inc.",
            ExternalReferences: [], Evidence: []);
        var source = new SbomEmbeddedSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.CopyrightText).IsEqualTo("Copyright (c) 2020 Foo Inc.");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.SbomEmbedded);
    }

    [Test]
    public async Task TryResolveAsync_NoCopyright_IsUnresolved()
    {
        var component = new SbomComponent(
            "Foo", "1.0.0", null, LicenseExpression.FromId("MIT"),
            RawCopyright: null, ExternalReferences: [], Evidence: []);
        var source = new SbomEmbeddedSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
```

```csharp
// tests/Attributary.Resolution.Tests/SpdxCanonicalSourceTests.cs
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

public class SpdxCanonicalSourceTests
{
    [Test]
    public async Task TryResolveAsync_KnownSpdxId_ReturnsLicenseText()
    {
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new SpdxCanonicalSource();

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.LicenseText).Contains("MIT License");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.SpdxCanonical);
    }

    [Test]
    public async Task TryResolveAsync_UnknownSpdxId_IsUnresolved()
    {
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("Some-Obscure-License"), null, [], []);
        var source = new SpdxCanonicalSource();

        var result = await source.TryResolveAsync(component, "Some-Obscure-License", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: FAIL — none of the resolution types exist yet.

- [ ] **Step 5: Implement**

```csharp
// src/Attributary.Resolution/SourceResult.cs
using Attributary.Domain;

namespace Attributary.Resolution;

public sealed record SourceResult(
    bool Resolved,
    string? LicenseText,
    string? CopyrightText,
    string? NoticeText,
    FieldProvenance? Provenance)
{
    public static SourceResult Unresolved { get; } = new(false, null, null, null, null);
}
```

```csharp
// src/Attributary.Resolution/ILicenseSource.cs
using Attributary.Domain;

namespace Attributary.Resolution;

public interface ILicenseSource
{
    ResolutionSourceStrategy Strategy { get; }
    Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct);
}
```

```csharp
// src/Attributary.Resolution/Sources/SbomEmbeddedSource.cs
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SbomEmbeddedSource : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        if (component.RawCopyright is null)
            return Task.FromResult(SourceResult.Unresolved);

        var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, null, component.RawCopyright, null, provenance));
    }
}
```

```csharp
// src/Attributary.Resolution/Sources/SpdxCanonicalSource.cs
using System.Reflection;
using System.Text.Json;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed class SpdxCanonicalSource : ILicenseSource
{
    private static readonly Dictionary<string, string> LicenseTexts = LoadEmbeddedLicenseTexts();

    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SpdxCanonical;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        if (licenseId is null || !LicenseTexts.TryGetValue(licenseId, out var text))
            return Task.FromResult(SourceResult.Unresolved);

        var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, FromCache: false);
        return Task.FromResult(new SourceResult(true, text, null, null, provenance));
    }

    private static Dictionary<string, string> LoadEmbeddedLicenseTexts()
    {
        var assembly = typeof(SpdxCanonicalSource).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("spdx-subset.json"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — all 4 tests green.

- [ ] **Step 7: Commit**

```bash
git add src/Attributary.Domain src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): add provenance model and offline (SBOM/SPDX-canonical) sources"
```

---

## Task 9: License resolution — NuGet local cache and GitHub VCS sources

**Files:**
- Create: `src/Attributary.Resolution/Sources/NuGetLocalCacheSource.cs`
- Create: `src/Attributary.Resolution/Sources/GitHubVcsSource.cs`
- Test: `tests/Attributary.Resolution.Tests/NuGetLocalCacheSourceTests.cs`
- Test: `tests/Attributary.Resolution.Tests/GitHubVcsSourceTests.cs`

**Interfaces:**
- Consumes: `ILicenseSource`, `SourceResult`, `SbomComponent`, `ExternalReferenceType` (Tasks 4, 8).
- Produces:
  - `class NuGetLocalCacheSource(string globalPackagesFolderPath) : ILicenseSource`
  - `class GitHubVcsSource(HttpClient httpClient) : ILicenseSource`

- [ ] **Step 1: Write the failing test for the NuGet local cache source, using a temp-directory fixture instead of the real global packages folder**

```csharp
// tests/Attributary.Resolution.Tests/NuGetLocalCacheSourceTests.cs
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

public class NuGetLocalCacheSourceTests
{
    private static string CreateFakePackage(string id, string version, string copyright, string? licenseFileContent)
    {
        var root = Path.Combine(Path.GetTempPath(), "attributary-tests", Guid.NewGuid().ToString());
        var packageDir = Path.Combine(root, id.ToLowerInvariant(), version.ToLowerInvariant());
        Directory.CreateDirectory(packageDir);

        var licenseElement = licenseFileContent is null
            ? ""
            : "<license type=\"file\">LICENSE.txt</license>";

        File.WriteAllText(Path.Combine(packageDir, $"{id.ToLowerInvariant()}.nuspec"), $"""
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>{id}</id>
                <version>{version}</version>
                <copyright>{copyright}</copyright>
                {licenseElement}
              </metadata>
            </package>
            """);

        if (licenseFileContent is not null)
            File.WriteAllText(Path.Combine(packageDir, "LICENSE.txt"), licenseFileContent);

        return root;
    }

    [Test]
    public async Task TryResolveAsync_NuspecWithCopyrightAndLicenseFile_ResolvesBoth()
    {
        var root = CreateFakePackage("Foo.Bar", "1.2.3", "Copyright (c) Foo Corp", "MIT License full text");
        var component = new SbomComponent("Foo.Bar", "1.2.3", "pkg:nuget/Foo.Bar@1.2.3", LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(root);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsTrue();
        await Assert.That(result.CopyrightText).IsEqualTo("Copyright (c) Foo Corp");
        await Assert.That(result.LicenseText).IsEqualTo("MIT License full text");
        await Assert.That(result.Provenance!.Strategy).IsEqualTo(ResolutionSourceStrategy.LocalPackageCache);
    }

    [Test]
    public async Task TryResolveAsync_PackageNotInCache_IsUnresolved()
    {
        var root = Path.Combine(Path.GetTempPath(), "attributary-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        var component = new SbomComponent("Missing.Package", "1.0.0", "pkg:nuget/Missing.Package@1.0.0", LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(root);

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }

    [Test]
    public async Task TryResolveAsync_NonNuGetComponent_IsUnresolved()
    {
        var component = new SbomComponent("libcurl", "8.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);
        var source = new NuGetLocalCacheSource(Path.GetTempPath());

        var result = await source.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(result.Resolved).IsFalse();
    }
}
```

- [ ] **Step 2: Write the failing test for the GitHub VCS source, using a fake `HttpMessageHandler`**

```csharp
// tests/Attributary.Resolution.Tests/GitHubVcsSourceTests.cs
using System.Net;
using System.Text;
using Attributary.Domain;

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
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: FAIL — `NuGetLocalCacheSource`, `GitHubVcsSource` do not exist.

- [ ] **Step 4: Implement**

```csharp
// src/Attributary.Resolution/Sources/NuGetLocalCacheSource.cs
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
```

```csharp
// src/Attributary.Resolution/Sources/GitHubVcsSource.cs
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Attributary.Domain;

namespace Attributary.Resolution.Sources;

public sealed partial class GitHubVcsSource(HttpClient httpClient) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.VcsRepository;

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
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — all 6 tests green.

- [ ] **Step 6: Commit**

```bash
git add src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): add NuGet local cache and GitHub VCS sources"
```

---

## Task 10: License resolution — fallback chain composition

**Files:**
- Create: `src/Attributary.Resolution/ILicenseResolutionChain.cs`
- Create: `src/Attributary.Resolution/LicenseResolutionChain.cs`
- Create: `src/Attributary.Resolution/DiagnosticDescriptors.cs`
- Test: `tests/Attributary.Resolution.Tests/LicenseResolutionChainTests.cs`

**Interfaces:**
- Consumes: `ILicenseSource`, `SourceResult` (Task 8-9); `IDiagnosticSink`, `DiagnosticDescriptor`, `DiagnosticSeverity` (Tasks 2-3); `LicenseResolution`, `ResolutionProvenance` (Task 8).
- Produces:
  - `static class ResolutionDiagnostics` with `OrExpressionUnresolved` descriptor (`ATT2001`).
  - `interface ILicenseResolutionChain { Task<LicenseResolution> ResolveAsync(SbomComponent component, IDiagnosticSink diagnostics, CancellationToken ct); }`
  - `class LicenseResolutionChain(IReadOnlyList<ILicenseSource> sources) : ILicenseResolutionChain`

**Scope note:** per the spec (§5), an unresolved *OR* expression stops at this stage with a diagnostic — that's the only diagnostic this stage raises. Whether a missing license text or copyright is actually a *problem* depends on what the matched license's rule requires (e.g. Unlicense/CC0 don't require copyright at all, per Task 7's default rules), and this stage runs before rule matching, so it cannot know that yet. The "required data obligation unresolved" check (`ATT3010`) belongs after `ObligationPlanBuilder` produces a plan — that's Task 20's `GenerateOrchestrator`, which has both the resolution and the matched rule's obligations in hand. Multi-license *AND* expressions are simplified the same way as OR for v1 (diagnostic asking for a single resolved id) rather than carrying a list through `LicenseResolution` — `IRuleMatcher.MatchExpression` (Task 6) already exists and is tested for when true multi-id support is added later; wiring it end-to-end is a v2 scope increase, not a v1 requirement.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Resolution.Tests/LicenseResolutionChainTests.cs
using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

file sealed class FakeSource(ResolutionSourceStrategy strategy, SourceResult result) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => strategy;
    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        => Task.FromResult(result);
}

public class LicenseResolutionChainTests
{
    private static SbomComponent BuildComponent(LicenseExpression license) =>
        new("Foo", "1.0.0", null, license, null, [], []);

    [Test]
    public async Task ResolveAsync_FirstSourceResolvesLicenseText_StopsAtFirstMatch()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SbomEmbedded, null, DateTimeOffset.UtcNow, false);
        var sources = new ILicenseSource[]
        {
            new FakeSource(ResolutionSourceStrategy.SbomEmbedded, new SourceResult(true, "MIT text", "Copyright X", null, provenance)),
            new FakeSource(ResolutionSourceStrategy.SpdxCanonical, new SourceResult(true, "different text", null, null, provenance))
        };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseText).IsEqualTo("MIT text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright X");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_NoSourceResolves_LeavesFieldsNullWithoutReportingADiagnostic()
    {
        var sources = new ILicenseSource[] { new FakeSource(ResolutionSourceStrategy.SbomEmbedded, SourceResult.Unresolved) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseText).IsNull();
        await Assert.That(resolution.CopyrightText).IsNull();
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_UnresolvedOrExpression_ReportsDiagnosticAndDoesNotCallSources()
    {
        var callCount = 0;
        var sources = new ILicenseSource[] { new CountingFakeSource(() => callCount++) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromExpression("(MIT OR Apache-2.0)")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseId).IsNull();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
    }

    private sealed class CountingFakeSource(Action onCalled) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            onCalled();
            return Task.FromResult(SourceResult.Unresolved);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: FAIL — `LicenseResolutionChain` does not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Resolution/DiagnosticDescriptors.cs
using Attributary.Diagnostics;

namespace Attributary.Resolution;

public static class ResolutionDiagnostics
{
    public static readonly DiagnosticDescriptor OrExpressionUnresolved =
        new("ATT2001", DiagnosticSeverity.Error, "Unresolved SPDX license expression");
}
```

```csharp
// src/Attributary.Resolution/ILicenseResolutionChain.cs
using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution;

public interface ILicenseResolutionChain
{
    Task<LicenseResolution> ResolveAsync(SbomComponent component, IDiagnosticSink diagnostics, CancellationToken ct);
}
```

```csharp
// src/Attributary.Resolution/LicenseResolutionChain.cs
using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution;

public sealed class LicenseResolutionChain(IReadOnlyList<ILicenseSource> sources) : ILicenseResolutionChain
{
    public async Task<LicenseResolution> ResolveAsync(SbomComponent component, IDiagnosticSink diagnostics, CancellationToken ct)
    {
        var context = $"{component.Name} {component.Version}";
        var declared = component.DeclaredLicense;

        if (declared.SpdxExpression is { } expression)
        {
            diagnostics.Report(ResolutionDiagnostics.OrExpressionUnresolved,
                $"Component declares an unresolved license expression '{expression}'; enrich the SBOM with a single license id.", context);
            return new LicenseResolution(component, null, null, null, null);
        }

        var licenseId = declared.SpdxId ?? declared.FreeTextName;

        string? licenseText = null, copyrightText = null, noticeText = null;
        FieldProvenance? licenseProvenance = null, copyrightProvenance = null, noticeProvenance = null;

        foreach (var source in sources)
        {
            var result = await source.TryResolveAsync(component, licenseId, ct);
            if (!result.Resolved) continue;

            if (licenseText is null && result.LicenseText is not null) { licenseText = result.LicenseText; licenseProvenance = result.Provenance; }
            if (copyrightText is null && result.CopyrightText is not null) { copyrightText = result.CopyrightText; copyrightProvenance = result.Provenance; }
            if (noticeText is null && result.NoticeText is not null) { noticeText = result.NoticeText; noticeProvenance = result.Provenance; }

            if (licenseText is not null && copyrightText is not null) break;
        }

        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);
        return new LicenseResolution(component, licenseId, copyrightText, licenseText, noticeText, provenance);
    }
}
```

Note: `context` is computed but only used by the OR-expression branch above; that's expected — this method no longer reports diagnostics for plain unresolved fields (see the scope note above the steps).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): compose the fallback chain and stop on unresolved expressions"
```

---

## Task 11: License cache store with integrity checking

**Files:**
- Create: `src/Attributary.Resolution/Caching/CacheKey.cs`
- Create: `src/Attributary.Resolution/Caching/CacheEntry.cs`
- Create: `src/Attributary.Resolution/Caching/ILicenseCacheStore.cs`
- Create: `src/Attributary.Resolution/Caching/FileSystemLicenseCacheStore.cs`
- Test: `tests/Attributary.Resolution.Tests/Caching/FileSystemLicenseCacheStoreTests.cs`

**Interfaces:**
- Consumes: `ResolutionSourceStrategy` (Task 8).
- Produces:
  - `record CacheKey(ResolutionSourceStrategy Strategy, string Discriminator)`
  - `record CacheEntry(string Content, string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc)`
  - `interface ILicenseCacheStore { string RootPath { get; } CacheEntry? TryGet(CacheKey key); void Put(CacheKey key, CacheEntry entry); void Clear(); IReadOnlyList<CacheKey> List(); }`
  - `class FileSystemLicenseCacheStore(string rootPath) : ILicenseCacheStore`, with `static string ComputeSha256(string content)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Resolution.Tests/Caching/FileSystemLicenseCacheStoreTests.cs
using Attributary.Domain;
using Attributary.Resolution.Caching;

namespace Attributary.Resolution.Tests.Caching;

public class FileSystemLicenseCacheStoreTests
{
    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "attributary-cache-tests", Guid.NewGuid().ToString());

    [Test]
    public async Task Put_ThenTryGet_RoundTripsContent()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        var entry = new CacheEntry("MIT text", FileSystemLicenseCacheStore.ComputeSha256("MIT text"), null, DateTimeOffset.UtcNow);

        store.Put(key, entry);
        var result = store.TryGet(key);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Content).IsEqualTo("MIT text");
    }

    [Test]
    public async Task TryGet_MissingKey_ReturnsNull()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var result = store.TryGet(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "Nonexistent"));
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task TryGet_TamperedContent_FailsIntegrityCheckAndReturnsNull()
    {
        var root = NewTempRoot();
        var store = new FileSystemLicenseCacheStore(root);
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        store.Put(key, new CacheEntry("original text", FileSystemLicenseCacheStore.ComputeSha256("original text"), null, DateTimeOffset.UtcNow));

        var contentPath = Directory.GetFiles(root, "*.content", SearchOption.AllDirectories).Single();
        File.WriteAllText(contentPath, "tampered text");

        var result = store.TryGet(key);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Clear_RemovesAllEntries()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var key = new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT");
        store.Put(key, new CacheEntry("text", FileSystemLicenseCacheStore.ComputeSha256("text"), null, DateTimeOffset.UtcNow));

        store.Clear();

        await Assert.That(store.TryGet(key)).IsNull();
        await Assert.That(store.List()).IsEmpty();
    }

    [Test]
    public async Task List_ReturnsAllStoredKeys()
    {
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        store.Put(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT"), new CacheEntry("a", FileSystemLicenseCacheStore.ComputeSha256("a"), null, DateTimeOffset.UtcNow));
        store.Put(new CacheKey(ResolutionSourceStrategy.VcsRepository, "pkg:nuget/Foo@1.0.0"), new CacheEntry("b", FileSystemLicenseCacheStore.ComputeSha256("b"), null, DateTimeOffset.UtcNow));

        var keys = store.List();

        await Assert.That(keys).HasCount().EqualTo(2);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: FAIL — `CacheKey`, `CacheEntry`, `ILicenseCacheStore`, `FileSystemLicenseCacheStore` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Resolution/Caching/CacheKey.cs
using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed record CacheKey(ResolutionSourceStrategy Strategy, string Discriminator);
```

```csharp
// src/Attributary.Resolution/Caching/CacheEntry.cs
namespace Attributary.Resolution.Caching;

public sealed record CacheEntry(string Content, string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc);
```

```csharp
// src/Attributary.Resolution/Caching/ILicenseCacheStore.cs
namespace Attributary.Resolution.Caching;

public interface ILicenseCacheStore
{
    string RootPath { get; }
    CacheEntry? TryGet(CacheKey key);
    void Put(CacheKey key, CacheEntry entry);
    void Clear();
    IReadOnlyList<CacheKey> List();
}
```

```csharp
// src/Attributary.Resolution/Caching/FileSystemLicenseCacheStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed class FileSystemLicenseCacheStore(string rootPath) : ILicenseCacheStore
{
    public string RootPath => rootPath;

    public CacheEntry? TryGet(CacheKey key)
    {
        var (contentPath, sidecarPath) = GetPaths(key);
        if (!File.Exists(contentPath) || !File.Exists(sidecarPath))
            return null;

        var sidecar = JsonSerializer.Deserialize<CacheSidecar>(File.ReadAllText(sidecarPath))!;
        var content = File.ReadAllText(contentPath);

        if (ComputeSha256(content) != sidecar.Sha256)
            return null;

        return new CacheEntry(content, sidecar.Sha256, sidecar.SourceUrl, sidecar.FetchedAtUtc);
    }

    public void Put(CacheKey key, CacheEntry entry)
    {
        var (contentPath, sidecarPath) = GetPaths(key);
        Directory.CreateDirectory(Path.GetDirectoryName(contentPath)!);
        File.WriteAllText(contentPath, entry.Content);
        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(new CacheSidecar(entry.Sha256, entry.SourceUrl, entry.FetchedAtUtc)));
    }

    public void Clear()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }

    public IReadOnlyList<CacheKey> List()
    {
        if (!Directory.Exists(rootPath)) return [];

        return Directory.GetFiles(rootPath, "*.sidecar.json", SearchOption.AllDirectories)
            .Select(sidecarPath =>
            {
                var strategy = Enum.Parse<ResolutionSourceStrategy>(Path.GetFileName(Path.GetDirectoryName(sidecarPath))!);
                var discriminator = Path.GetFileName(sidecarPath)[..^".sidecar.json".Length];
                return new CacheKey(strategy, discriminator);
            })
            .ToList();
    }

    private (string contentPath, string sidecarPath) GetPaths(CacheKey key)
    {
        var safeName = string.Join("_", key.Discriminator.Split(Path.GetInvalidFileNameChars()));
        var dir = Path.Combine(rootPath, key.Strategy.ToString());
        return (Path.Combine(dir, $"{safeName}.content"), Path.Combine(dir, $"{safeName}.sidecar.json"));
    }

    public static string ComputeSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    private sealed record CacheSidecar(string Sha256, string? SourceUrl, DateTimeOffset FetchedAtUtc);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): add file-system license cache store with integrity checking"
```

---

## Task 12: Wire caching into the resolution chain

**Files:**
- Create: `src/Attributary.Resolution/Caching/CachingLicenseSource.cs`
- Test: `tests/Attributary.Resolution.Tests/Caching/CachingLicenseSourceTests.cs`

**Interfaces:**
- Consumes: `ILicenseSource`, `SourceResult` (Task 8); `ILicenseCacheStore`, `CacheKey`, `CacheEntry`, `FileSystemLicenseCacheStore.ComputeSha256` (Task 11).
- Produces: `class CachingLicenseSource(ILicenseSource inner, ILicenseCacheStore store) : ILicenseSource`. Only license *text* is cached (the only field the SPDX-canonical and VCS sources produce); `--no-cache` is a CLI-layer decision (Task 21) about whether to wrap a source with this decorator at all — this class has no on/off switch itself.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Resolution.Tests/Caching/CachingLicenseSourceTests.cs
using Attributary.Domain;
using Attributary.Resolution.Caching;

namespace Attributary.Resolution.Tests.Caching;

file sealed class CountingSource(SourceResult result, ResolutionSourceStrategy strategy) : ILicenseSource
{
    public int CallCount { get; private set; }
    public ResolutionSourceStrategy Strategy => strategy;

    public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

public class CachingLicenseSourceTests
{
    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "attributary-cache-tests", Guid.NewGuid().ToString());

    [Test]
    public async Task TryResolveAsync_SecondCallForSameSpdxId_HitsCacheNotInnerSource()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var inner = new CountingSource(new SourceResult(true, "MIT text", null, null, provenance), ResolutionSourceStrategy.SpdxCanonical);
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var caching = new CachingLicenseSource(inner, store);
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);

        var first = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);
        var second = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(first.LicenseText).IsEqualTo("MIT text");
        await Assert.That(second.LicenseText).IsEqualTo("MIT text");
        await Assert.That(inner.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task TryResolveAsync_CacheHit_MarksProvenanceFromCache()
    {
        var provenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var inner = new CountingSource(new SourceResult(true, "MIT text", null, null, provenance), ResolutionSourceStrategy.SpdxCanonical);
        var store = new FileSystemLicenseCacheStore(NewTempRoot());
        var caching = new CachingLicenseSource(inner, store);
        var component = new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromId("MIT"), null, [], []);

        await caching.TryResolveAsync(component, "MIT", CancellationToken.None);
        var second = await caching.TryResolveAsync(component, "MIT", CancellationToken.None);

        await Assert.That(second.Provenance!.FromCache).IsTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: FAIL — `CachingLicenseSource` does not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Resolution/Caching/CachingLicenseSource.cs
using Attributary.Domain;

namespace Attributary.Resolution.Caching;

public sealed class CachingLicenseSource(ILicenseSource inner, ILicenseCacheStore store) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => inner.Strategy;

    public async Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
        var discriminator = inner.Strategy == ResolutionSourceStrategy.SpdxCanonical
            ? licenseId
            : component.Purl is not null ? $"{component.Purl}@{component.Version}" : null;

        if (discriminator is null)
            return await inner.TryResolveAsync(component, licenseId, ct);

        var key = new CacheKey(inner.Strategy, discriminator);
        var cached = store.TryGet(key);
        if (cached is not null)
        {
            var cachedProvenance = new FieldProvenance(inner.Strategy, cached.SourceUrl, cached.FetchedAtUtc, FromCache: true);
            return new SourceResult(true, cached.Content, null, null, cachedProvenance);
        }

        var result = await inner.TryResolveAsync(component, licenseId, ct);
        if (result.Resolved && result.LicenseText is not null)
        {
            var sha = FileSystemLicenseCacheStore.ComputeSha256(result.LicenseText);
            store.Put(key, new CacheEntry(result.LicenseText, sha, result.Provenance?.SourceUrl, DateTimeOffset.UtcNow));
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — both tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): cache license text via a decorator over any source"
```

---

## Task 13: Artifact generation — license texts and notice documents

**Files:**
- Create: `src/Attributary.Artifacts/LicenseTextsDocument.cs`
- Create: `src/Attributary.Artifacts/LicenseTextsDocumentBuilder.cs`
- Create: `src/Attributary.Artifacts/NoticeDocument.cs`
- Create: `src/Attributary.Artifacts/NoticeDocumentBuilder.cs`
- Test: `tests/Attributary.Artifacts.Tests/LicenseTextsDocumentBuilderTests.cs`
- Test: `tests/Attributary.Artifacts.Tests/NoticeDocumentBuilderTests.cs`

**Interfaces:**
- Consumes: `ObligationPlan`, `ObligationKind`, `Obligation` (Task 6); `LicenseResolution` (Task 8).
- Produces:
  - `record LicenseTextEntry(string LicenseId, string Text)`
  - `record LicenseTextsDocument(IReadOnlyList<LicenseTextEntry> Licenses)`
  - `static class LicenseTextsDocumentBuilder { static LicenseTextsDocument Build(IReadOnlyList<ObligationPlan> plans); }`
  - `record NoticeSection(string ComponentName, string ComponentVersion, string NoticeText)`
  - `record NoticeDocument(IReadOnlyList<NoticeSection> Sections)`
  - `static class NoticeDocumentBuilder { static NoticeDocument Build(IReadOnlyList<ObligationPlan> plans); }`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Artifacts.Tests/LicenseTextsDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class LicenseTextsDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string licenseText, params ObligationKind[] obligations) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), "Copyright X", [], []),
            licenseId, "Copyright X", licenseText, null),
        LicensePolicy.Allow,
        obligations.Select(k => new Obligation(k, null)).ToList(),
        []);

    [Test]
    public async Task Build_TwoComponentsSharingLicense_DedupesIntoOneEntry()
    {
        var plans = new[]
        {
            BuildPlan("Foo", "MIT", "MIT text", ObligationKind.Copyright, ObligationKind.LicenseText),
            BuildPlan("Bar", "MIT", "MIT text", ObligationKind.Copyright, ObligationKind.LicenseText)
        };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).HasCount().EqualTo(1);
        await Assert.That(document.Licenses[0].LicenseId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Build_ComponentWithoutLicenseTextObligation_IsExcluded()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "MIT text", ObligationKind.Copyright) };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).IsEmpty();
    }
}
```

```csharp
// tests/Attributary.Artifacts.Tests/NoticeDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class NoticeDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string? noticeText, params ObligationKind[] obligations) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId("Apache-2.0"), "Copyright X", [], []),
            "Apache-2.0", "Copyright X", "Apache text", noticeText),
        LicensePolicy.Allow,
        obligations.Select(k => new Obligation(k, null)).ToList(),
        []);

    [Test]
    public async Task Build_ComponentWithNoticeObligation_IncludesSection()
    {
        var plans = new[] { BuildPlan("Foo", "Foo notice text", ObligationKind.Copyright, ObligationKind.LicenseText, ObligationKind.NoticeText) };

        var document = NoticeDocumentBuilder.Build(plans);

        await Assert.That(document.Sections).HasCount().EqualTo(1);
        await Assert.That(document.Sections[0].NoticeText).IsEqualTo("Foo notice text");
    }

    [Test]
    public async Task Build_ComponentWithoutNoticeObligation_IsExcluded()
    {
        var plans = new[] { BuildPlan("Foo", null, ObligationKind.Copyright, ObligationKind.LicenseText) };

        var document = NoticeDocumentBuilder.Build(plans);

        await Assert.That(document.Sections).IsEmpty();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: FAIL — none of the artifact types exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Artifacts/LicenseTextsDocument.cs
namespace Attributary.Artifacts;

public sealed record LicenseTextEntry(string LicenseId, string Text);
public sealed record LicenseTextsDocument(IReadOnlyList<LicenseTextEntry> Licenses);
```

```csharp
// src/Attributary.Artifacts/LicenseTextsDocumentBuilder.cs
using Attributary.Rules;

namespace Attributary.Artifacts;

public static class LicenseTextsDocumentBuilder
{
    public static LicenseTextsDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.LicenseText)
                && p.Resolution.ResolvedLicenseId is not null
                && p.Resolution.LicenseText is not null)
            .GroupBy(p => p.Resolution.ResolvedLicenseId!)
            .Select(g => new LicenseTextEntry(g.Key, g.First().Resolution.LicenseText!))
            .OrderBy(e => e.LicenseId, StringComparer.Ordinal)
            .ToList();

        return new LicenseTextsDocument(entries);
    }
}
```

```csharp
// src/Attributary.Artifacts/NoticeDocument.cs
namespace Attributary.Artifacts;

public sealed record NoticeSection(string ComponentName, string ComponentVersion, string NoticeText);
public sealed record NoticeDocument(IReadOnlyList<NoticeSection> Sections);
```

```csharp
// src/Attributary.Artifacts/NoticeDocumentBuilder.cs
using Attributary.Rules;

namespace Attributary.Artifacts;

public static class NoticeDocumentBuilder
{
    public static NoticeDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var sections = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.NoticeText) && p.Resolution.NoticeText is not null)
            .Select(p => new NoticeSection(p.Resolution.Component.Name, p.Resolution.Component.Version, p.Resolution.NoticeText!))
            .OrderBy(s => s.ComponentName, StringComparer.Ordinal)
            .ToList();

        return new NoticeDocument(sections);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Artifacts tests/Attributary.Artifacts.Tests
git commit -m "feat(artifacts): build deduped license-texts and aggregated notice documents"
```

---

## Task 14: Artifact generation — attribution document

**Files:**
- Create: `src/Attributary.Artifacts/AttributionDocument.cs`
- Create: `src/Attributary.Artifacts/AttributionDocumentBuilder.cs`
- Test: `tests/Attributary.Artifacts.Tests/AttributionDocumentBuilderTests.cs`

**Interfaces:**
- Consumes: `ObligationPlan`, `LicenseResolution` (Tasks 6, 8).
- Produces:
  - `record AttributionRow(string ComponentName, string ComponentVersion, string LicenseId, string Copyright)`
  - `record AttributionDocument(IReadOnlyList<AttributionRow> Rows, IReadOnlyDictionary<string, string> LicenseTextsById, bool GroupByLicense, bool EmbedLicenseText)`
  - `static class AttributionDocumentBuilder { static AttributionDocument Build(IReadOnlyList<ObligationPlan> plans, bool groupByLicense, bool embedLicenseText); }`

Note: `LicenseTextsById` is always populated regardless of `EmbedLicenseText` — that flag is a rendering decision for the writer (Tasks 16-19), not something the builder should filter on.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Artifacts.Tests/AttributionDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class AttributionDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string copyright, string licenseText) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), copyright, [], []),
            licenseId, copyright, licenseText, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    [Test]
    public async Task Build_AlwaysIncludesLicenseIdPerRow_RegardlessOfGrouping()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var flat = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);
        var grouped = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(flat.Rows[0].LicenseId).IsEqualTo("MIT");
        await Assert.That(grouped.Rows[0].LicenseId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Build_LicenseTextsById_AlwaysPopulatedRegardlessOfEmbedFlag()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(document.LicenseTextsById["MIT"]).IsEqualTo("MIT text");
        await Assert.That(document.EmbedLicenseText).IsFalse();
    }

    [Test]
    public async Task Build_RowsSortedByComponentName()
    {
        var plans = new[]
        {
            BuildPlan("Zeta", "MIT", "Copyright Zeta", "MIT text"),
            BuildPlan("Alpha", "MIT", "Copyright Alpha", "MIT text")
        };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);

        await Assert.That(document.Rows[0].ComponentName).IsEqualTo("Alpha");
        await Assert.That(document.Rows[1].ComponentName).IsEqualTo("Zeta");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: FAIL — `AttributionDocument`/`AttributionDocumentBuilder` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Artifacts/AttributionDocument.cs
namespace Attributary.Artifacts;

public sealed record AttributionRow(string ComponentName, string ComponentVersion, string LicenseId, string Copyright);

public sealed record AttributionDocument(
    IReadOnlyList<AttributionRow> Rows,
    IReadOnlyDictionary<string, string> LicenseTextsById,
    bool GroupByLicense,
    bool EmbedLicenseText);
```

```csharp
// src/Attributary.Artifacts/AttributionDocumentBuilder.cs
using Attributary.Rules;

namespace Attributary.Artifacts;

public static class AttributionDocumentBuilder
{
    public static AttributionDocument Build(IReadOnlyList<ObligationPlan> plans, bool groupByLicense, bool embedLicenseText)
    {
        var rows = plans
            .Select(p => new AttributionRow(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseId ?? "UNKNOWN",
                p.Resolution.CopyrightText ?? ""))
            .OrderBy(r => r.ComponentName, StringComparer.Ordinal)
            .ToList();

        var licenseTexts = plans
            .Where(p => p.Resolution.ResolvedLicenseId is not null && p.Resolution.LicenseText is not null)
            .GroupBy(p => p.Resolution.ResolvedLicenseId!)
            .ToDictionary(g => g.Key, g => g.First().Resolution.LicenseText!);

        return new AttributionDocument(rows, licenseTexts, groupByLicense, embedLicenseText);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Artifacts tests/Attributary.Artifacts.Tests
git commit -m "feat(artifacts): build the attribution document with grouping and embed options"
```

---

## Task 15: Artifact generation — compliance report document

**Files:**
- Create: `src/Attributary.Artifacts/ComplianceReportDocument.cs`
- Create: `src/Attributary.Artifacts/ComplianceReportDocumentBuilder.cs`
- Test: `tests/Attributary.Artifacts.Tests/ComplianceReportDocumentBuilderTests.cs`

**Interfaces:**
- Consumes: `ObligationPlan`, `LicensePolicy`, `ObligationFlag` (Task 6); `ResolutionProvenance`, `ResolutionSourceStrategy` (Task 8).
- Produces:
  - `record ComplianceReportEntry(string ComponentName, string ComponentVersion, string LicenseId, ResolutionSourceStrategy? LicenseTextSource, IReadOnlyList<ObligationKind> SatisfiedObligations)`
  - `record ReviewFlagEntry(string ComponentName, string ComponentVersion, string LicenseId, IReadOnlyList<ObligationFlag> Flags, LicensePolicy Policy)`
  - `record ComplianceReportDocument(IReadOnlyList<ComplianceReportEntry> Entries, IReadOnlyList<ReviewFlagEntry> FlaggedForReview)`
  - `static class ComplianceReportDocumentBuilder { static ComplianceReportDocument Build(IReadOnlyList<ObligationPlan> plans); }`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Artifacts.Tests/ComplianceReportDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class ComplianceReportDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, LicensePolicy policy, IReadOnlyList<ObligationFlag> flags) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId("GPL-3.0-only"), "Copyright X", [], []),
            "GPL-3.0-only", "Copyright X", "GPL text", null,
            new ResolutionProvenance(new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false), null, null)),
        policy,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        flags);

    [Test]
    public async Task Build_EveryPlan_HasAnEntryWithSatisfiedObligations()
    {
        var plans = new[] { BuildPlan("Foo", LicensePolicy.Allow, []) };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.Entries).HasCount().EqualTo(1);
        await Assert.That(report.Entries[0].SatisfiedObligations).Contains(ObligationKind.Copyright);
        await Assert.That(report.Entries[0].LicenseTextSource).IsEqualTo(ResolutionSourceStrategy.SpdxCanonical);
    }

    [Test]
    public async Task Build_PlanWithFlagsOrNonAllowPolicy_AppearsInFlaggedForReview()
    {
        var plans = new[]
        {
            BuildPlan("Foo", LicensePolicy.Warn, [ObligationFlag.SourceOffer, ObligationFlag.CopyleftStrong]),
            BuildPlan("Bar", LicensePolicy.Allow, [])
        };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.FlaggedForReview).HasCount().EqualTo(1);
        await Assert.That(report.FlaggedForReview[0].ComponentName).IsEqualTo("Foo");
        await Assert.That(report.FlaggedForReview[0].Flags).Contains(ObligationFlag.SourceOffer);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: FAIL — compliance report types do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Artifacts/ComplianceReportDocument.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts;

public sealed record ComplianceReportEntry(
    string ComponentName,
    string ComponentVersion,
    string LicenseId,
    ResolutionSourceStrategy? LicenseTextSource,
    IReadOnlyList<ObligationKind> SatisfiedObligations);

public sealed record ReviewFlagEntry(
    string ComponentName,
    string ComponentVersion,
    string LicenseId,
    IReadOnlyList<ObligationFlag> Flags,
    LicensePolicy Policy);

public sealed record ComplianceReportDocument(
    IReadOnlyList<ComplianceReportEntry> Entries,
    IReadOnlyList<ReviewFlagEntry> FlaggedForReview);
```

```csharp
// src/Attributary.Artifacts/ComplianceReportDocumentBuilder.cs
using Attributary.Rules;

namespace Attributary.Artifacts;

public static class ComplianceReportDocumentBuilder
{
    public static ComplianceReportDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans.Select(p => new ComplianceReportEntry(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseId ?? "UNKNOWN",
                p.Resolution.Provenance?.LicenseTextProvenance?.Strategy,
                p.Obligations.Select(o => o.Kind).ToList()))
            .ToList();

        var flagged = plans
            .Where(p => p.Flags.Count > 0 || p.Policy != LicensePolicy.Allow)
            .Select(p => new ReviewFlagEntry(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseId ?? "UNKNOWN",
                p.Flags,
                p.Policy))
            .ToList();

        return new ComplianceReportDocument(entries, flagged);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: PASS — both tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Artifacts tests/Attributary.Artifacts.Tests
git commit -m "feat(artifacts): build the compliance report document"
```

---

## Task 16: Output writers — plain text

**Files:**
- Create: `src/Attributary.Output/OutputFormat.cs`
- Create: `src/Attributary.Output/INoticeWriter.cs`
- Create: `src/Attributary.Output/IAttributionWriter.cs`
- Create: `src/Attributary.Output/IComplianceReportWriter.cs`
- Create: `src/Attributary.Output/Text/PlainTextNoticeWriter.cs`
- Create: `src/Attributary.Output/Text/TxtAttributionWriter.cs`
- Create: `src/Attributary.Output/Text/TxtComplianceReportWriter.cs`
- Test: `tests/Attributary.Output.Tests/Text/TxtAttributionWriterTests.cs`
- Test: `tests/Attributary.Output.Tests/Text/TxtComplianceReportWriterTests.cs`
- Test: `tests/Attributary.Output.Tests/Text/PlainTextNoticeWriterTests.cs`

**Interfaces:**
- Consumes: `AttributionDocument`, `NoticeDocument`, `ComplianceReportDocument` and their row/entry types (Tasks 13-15).
- Produces:
  - `enum OutputFormat { Txt, Md, Json, Html }`
  - `interface INoticeWriter { string Render(NoticeDocument document); }`
  - `interface IAttributionWriter { OutputFormat Format { get; } string Render(AttributionDocument document); }`
  - `interface IComplianceReportWriter { OutputFormat Format { get; } string Render(ComplianceReportDocument document); }`
  - `class PlainTextNoticeWriter : INoticeWriter`
  - `class TxtAttributionWriter : IAttributionWriter`
  - `class TxtComplianceReportWriter : IComplianceReportWriter`

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Output.Tests/Text/TxtAttributionWriterTests.cs
using Attributary.Artifacts;

namespace Attributary.Output.Tests.Text;

public class TxtAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithEmbed_ShowsLicenseHeadingAndFullText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: true);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("License: MIT");
        await Assert.That(result).Contains("Foo 1.0.0 - Copyright Foo");
        await Assert.That(result).Contains("MIT full text");
    }

    [Test]
    public async Task Render_FlatWithoutEmbed_ShowsRowsButNoLicenseText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT | Copyright Foo");
        await Assert.That(result).DoesNotContain("MIT full text");
    }
}
```

```csharp
// tests/Attributary.Output.Tests/Text/TxtComplianceReportWriterTests.cs
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Output.Tests.Text;

public class TxtComplianceReportWriterTests
{
    [Test]
    public async Task Render_IncludesEntriesAndFlaggedForReviewSections()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "GPL-3.0-only", ResolutionSourceStrategy.SpdxCanonical, [ObligationKind.Copyright])],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", "GPL-3.0-only", [ObligationFlag.SourceOffer], LicensePolicy.Warn)]);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - GPL-3.0-only (source: SpdxCanonical)");
        await Assert.That(result).Contains("policy: Warn, flags: SourceOffer");
    }
}
```

```csharp
// tests/Attributary.Output.Tests/Text/PlainTextNoticeWriterTests.cs
using Attributary.Artifacts;

namespace Attributary.Output.Tests.Text;

public class PlainTextNoticeWriterTests
{
    [Test]
    public async Task Render_OneSection_IncludesComponentHeaderAndText()
    {
        var document = new NoticeDocument([new NoticeSection("Foo", "1.0.0", "Foo notice text")]);
        var writer = new PlainTextNoticeWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0");
        await Assert.That(result).Contains("Foo notice text");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: FAIL — none of the output types exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Output/OutputFormat.cs
namespace Attributary.Output;

public enum OutputFormat { Txt, Md, Json, Html }
```

```csharp
// src/Attributary.Output/INoticeWriter.cs
using Attributary.Artifacts;

namespace Attributary.Output;

public interface INoticeWriter
{
    string Render(NoticeDocument document);
}
```

```csharp
// src/Attributary.Output/IAttributionWriter.cs
using Attributary.Artifacts;

namespace Attributary.Output;

public interface IAttributionWriter
{
    OutputFormat Format { get; }
    string Render(AttributionDocument document);
}
```

```csharp
// src/Attributary.Output/IComplianceReportWriter.cs
using Attributary.Artifacts;

namespace Attributary.Output;

public interface IComplianceReportWriter
{
    OutputFormat Format { get; }
    string Render(ComplianceReportDocument document);
}
```

```csharp
// src/Attributary.Output/Text/PlainTextNoticeWriter.cs
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class PlainTextNoticeWriter : INoticeWriter
{
    public string Render(NoticeDocument document)
    {
        var sb = new StringBuilder();
        foreach (var section in document.Sections)
        {
            sb.AppendLine(new string('-', 40));
            sb.AppendLine($"{section.ComponentName} {section.ComponentVersion}");
            sb.AppendLine(new string('-', 40));
            sb.AppendLine(section.NoticeText);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
```

```csharp
// src/Attributary.Output/Text/TxtAttributionWriter.cs
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class TxtAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Txt;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();

        if (document.GroupByLicense)
        {
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"License: {group.Key}");
                sb.AppendLine(new string('-', 40));
                foreach (var row in group)
                    sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} - {row.Copyright}");

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                {
                    sb.AppendLine();
                    sb.AppendLine(text);
                }
                sb.AppendLine();
            }
        }
        else
        {
            foreach (var row in document.Rows)
                sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} | {row.LicenseId} | {row.Copyright}");

            if (document.EmbedLicenseText)
            {
                sb.AppendLine();
                sb.AppendLine("Licenses");
                sb.AppendLine(new string('=', 40));
                foreach (var (licenseId, text) in document.LicenseTextsById.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    sb.AppendLine($"License: {licenseId}");
                    sb.AppendLine(text);
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }
}
```

```csharp
// src/Attributary.Output/Text/TxtComplianceReportWriter.cs
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Text;

public sealed class TxtComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Txt;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Compliance Report");
        sb.AppendLine(new string('=', 40));
        foreach (var entry in document.Entries)
            sb.AppendLine($"{entry.ComponentName} {entry.ComponentVersion} - {entry.LicenseId} (source: {(entry.LicenseTextSource?.ToString() ?? "unresolved")})");

        sb.AppendLine();
        sb.AppendLine("Flagged for review");
        sb.AppendLine(new string('-', 40));
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"{flag.ComponentName} {flag.ComponentVersion} - policy: {flag.Policy}, flags: {string.Join(", ", flag.Flags)}");

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Output tests/Attributary.Output.Tests
git commit -m "feat(output): add plain text writers for attribution, report, and notice"
```

---

## Task 17: Output writers — Markdown

**Files:**
- Create: `src/Attributary.Output/Markdown/MdAttributionWriter.cs`
- Create: `src/Attributary.Output/Markdown/MdComplianceReportWriter.cs`
- Test: `tests/Attributary.Output.Tests/Markdown/MdAttributionWriterTests.cs`
- Test: `tests/Attributary.Output.Tests/Markdown/MdComplianceReportWriterTests.cs`

**Interfaces:**
- Consumes: `IAttributionWriter`, `IComplianceReportWriter`, `OutputFormat` (Task 16); `AttributionDocument`, `ComplianceReportDocument` (Tasks 14-15).
- Produces: `class MdAttributionWriter : IAttributionWriter`, `class MdComplianceReportWriter : IComplianceReportWriter`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Output.Tests/Markdown/MdAttributionWriterTests.cs
using Attributary.Artifacts;

namespace Attributary.Output.Tests.Markdown;

public class MdAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithoutEmbed_LinksToLicensesFolder()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new MdAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("## MIT");
        await Assert.That(result).Contains("[View full license text](LICENSES/MIT.txt)");
        await Assert.That(result).DoesNotContain("MIT full text");
    }

    [Test]
    public async Task Render_FlatWithEmbed_UsesTableWithLinkedLicenseColumn()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: true);
        var writer = new MdAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("| Foo | 1.0.0 | [MIT](LICENSES/MIT.txt) | Copyright Foo |");
    }
}
```

```csharp
// tests/Attributary.Output.Tests/Markdown/MdComplianceReportWriterTests.cs
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Output.Tests.Markdown;

public class MdComplianceReportWriterTests
{
    [Test]
    public async Task Render_ProducesTablesForEntriesAndFlags()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", "MIT", [ObligationFlag.NonEndorsement], LicensePolicy.Allow)]);
        var writer = new MdComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("# Compliance Report");
        await Assert.That(result).Contains("| Foo | 1.0.0 | MIT |");
        await Assert.That(result).Contains("NonEndorsement");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: FAIL — `MdAttributionWriter`/`MdComplianceReportWriter` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Output/Markdown/MdAttributionWriter.cs
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Markdown;

public sealed class MdAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Md;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();

        if (document.GroupByLicense)
        {
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                foreach (var row in group)
                    sb.AppendLine($"- {row.ComponentName} {row.ComponentVersion} — {row.Copyright}");
                sb.AppendLine();

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(text);
                    sb.AppendLine("```");
                }
                else
                {
                    sb.AppendLine($"[View full license text](LICENSES/{group.Key}.txt)");
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("| Component | Version | License | Copyright |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var row in document.Rows)
                sb.AppendLine($"| {row.ComponentName} | {row.ComponentVersion} | [{row.LicenseId}](LICENSES/{row.LicenseId}.txt) | {row.Copyright} |");
        }

        return sb.ToString();
    }
}
```

```csharp
// src/Attributary.Output/Markdown/MdComplianceReportWriter.cs
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Markdown;

public sealed class MdComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Md;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Compliance Report");
        sb.AppendLine();
        sb.AppendLine("| Component | Version | License | Source |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var entry in document.Entries)
            sb.AppendLine($"| {entry.ComponentName} | {entry.ComponentVersion} | {entry.LicenseId} | {(entry.LicenseTextSource?.ToString() ?? "unresolved")} |");

        sb.AppendLine();
        sb.AppendLine("## Flagged for review");
        sb.AppendLine();
        sb.AppendLine("| Component | Version | License | Policy | Flags |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"| {flag.ComponentName} | {flag.ComponentVersion} | {flag.LicenseId} | {flag.Policy} | {string.Join(", ", flag.Flags)} |");

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Output tests/Attributary.Output.Tests
git commit -m "feat(output): add Markdown writers with license-file linking"
```

---

## Task 18: Output writers — JSON

**Files:**
- Create: `src/Attributary.Output/Json/JsonAttributionWriter.cs`
- Create: `src/Attributary.Output/Json/JsonComplianceReportWriter.cs`
- Test: `tests/Attributary.Output.Tests/Json/JsonAttributionWriterTests.cs`
- Test: `tests/Attributary.Output.Tests/Json/JsonComplianceReportWriterTests.cs`

**Interfaces:**
- Consumes: `IAttributionWriter`, `IComplianceReportWriter`, `OutputFormat` (Task 16); `AttributionDocument`, `ComplianceReportDocument` (Tasks 14-15).
- Produces: `class JsonAttributionWriter : IAttributionWriter`, `class JsonComplianceReportWriter : IComplianceReportWriter`. JSON output always fully structured (component list + license dictionary); `EmbedLicenseText` only controls whether the license dictionary's values carry full text or `null` — grouping never applies to JSON per spec §7.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Output.Tests/Json/JsonAttributionWriterTests.cs
using System.Text.Json;
using Attributary.Artifacts;

namespace Attributary.Output.Tests.Json;

public class JsonAttributionWriterTests
{
    [Test]
    public async Task Render_EmbedTrue_IncludesFullLicenseTextInPayload()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: true);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("licenses").GetProperty("MIT").GetString()).IsEqualTo("MIT full text");
    }

    [Test]
    public async Task Render_EmbedFalse_LicenseValueIsNull()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("licenses").GetProperty("MIT").ValueKind).IsEqualTo(JsonValueKind.Null);
    }
}
```

```csharp
// tests/Attributary.Output.Tests/Json/JsonComplianceReportWriterTests.cs
using System.Text.Json;
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Output.Tests.Json;

public class JsonComplianceReportWriterTests
{
    [Test]
    public async Task Render_SerializesEntriesWithEnumsAsStrings()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new JsonComplianceReportWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("entries")[0].GetProperty("licenseTextSource").GetString()).IsEqualTo("SbomEmbedded");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: FAIL — `JsonAttributionWriter`/`JsonComplianceReportWriter` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Output/Json/JsonAttributionWriter.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Attributary.Artifacts;

namespace Attributary.Output.Json;

public sealed class JsonAttributionWriter : IAttributionWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public OutputFormat Format => OutputFormat.Json;

    public string Render(AttributionDocument document)
    {
        var payload = new
        {
            components = document.Rows.Select(r => new { r.ComponentName, r.ComponentVersion, r.LicenseId, r.Copyright }),
            licenses = document.LicenseTextsById.ToDictionary(
                kv => kv.Key,
                kv => document.EmbedLicenseText ? kv.Value : (string?)null)
        };
        return JsonSerializer.Serialize(payload, Options);
    }
}
```

```csharp
// src/Attributary.Output/Json/JsonComplianceReportWriter.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using Attributary.Artifacts;

namespace Attributary.Output.Json;

public sealed class JsonComplianceReportWriter : IComplianceReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public OutputFormat Format => OutputFormat.Json;

    public string Render(ComplianceReportDocument document) => JsonSerializer.Serialize(document, Options);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Output tests/Attributary.Output.Tests
git commit -m "feat(output): add JSON writers with structured, fully-queryable payloads"
```

---

## Task 19: Output writers — HTML

**Files:**
- Create: `src/Attributary.Output/Html/HtmlAttributionWriter.cs`
- Create: `src/Attributary.Output/Html/HtmlComplianceReportWriter.cs`
- Test: `tests/Attributary.Output.Tests/Html/HtmlAttributionWriterTests.cs`
- Test: `tests/Attributary.Output.Tests/Html/HtmlComplianceReportWriterTests.cs`

**Interfaces:**
- Consumes: `IAttributionWriter`, `IComplianceReportWriter`, `OutputFormat` (Task 16); `AttributionDocument`, `ComplianceReportDocument` (Tasks 14-15).
- Produces: `class HtmlAttributionWriter : IAttributionWriter`, `class HtmlComplianceReportWriter : IComplianceReportWriter`. Component/copyright text is HTML-encoded before being embedded.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Attributary.Output.Tests/Html/HtmlAttributionWriterTests.cs
using Attributary.Artifacts;

namespace Attributary.Output.Tests.Html;

public class HtmlAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithoutEmbed_LinksToLicensesFolder()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new HtmlAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("<h2>MIT</h2>");
        await Assert.That(result).Contains("<a href=\"LICENSES/MIT.txt\">");
    }

    [Test]
    public async Task Render_EncodesHtmlSpecialCharactersInCopyright()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", "MIT", "Copyright <Foo & Co>")],
            LicenseTextsById: new Dictionary<string, string>(),
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new HtmlAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Copyright &lt;Foo &amp; Co&gt;");
    }
}
```

```csharp
// tests/Attributary.Output.Tests/Html/HtmlComplianceReportWriterTests.cs
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Output.Tests.Html;

public class HtmlComplianceReportWriterTests
{
    [Test]
    public async Task Render_ProducesHtmlTablesForEntriesAndFlags()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", "MIT", ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new HtmlComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("<h1>Compliance Report</h1>");
        await Assert.That(result).Contains("<td>Foo</td>");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: FAIL — `HtmlAttributionWriter`/`HtmlComplianceReportWriter` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Output/Html/HtmlAttributionWriter.cs
using System.Net;
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Html;

public sealed class HtmlAttributionWriter : IAttributionWriter
{
    public OutputFormat Format => OutputFormat.Html;

    public string Render(AttributionDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Third-Party Notices</h1>");

        if (document.GroupByLicense)
        {
            foreach (var group in document.Rows.GroupBy(r => r.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"<h2>{Encode(group.Key)}</h2>");
                sb.AppendLine("<ul>");
                foreach (var row in group)
                    sb.AppendLine($"<li>{Encode(row.ComponentName)} {Encode(row.ComponentVersion)} — {Encode(row.Copyright)}</li>");
                sb.AppendLine("</ul>");

                if (document.EmbedLicenseText && document.LicenseTextsById.TryGetValue(group.Key, out var text))
                    sb.AppendLine($"<pre>{Encode(text)}</pre>");
                else
                    sb.AppendLine($"<p><a href=\"LICENSES/{Encode(group.Key)}.txt\">View full license text</a></p>");
            }
        }
        else
        {
            sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Copyright</th></tr>");
            foreach (var row in document.Rows)
                sb.AppendLine($"<tr><td>{Encode(row.ComponentName)}</td><td>{Encode(row.ComponentVersion)}</td>"
                    + $"<td><a href=\"LICENSES/{Encode(row.LicenseId)}.txt\">{Encode(row.LicenseId)}</a></td><td>{Encode(row.Copyright)}</td></tr>");
            sb.AppendLine("</table>");
        }

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
```

```csharp
// src/Attributary.Output/Html/HtmlComplianceReportWriter.cs
using System.Net;
using System.Text;
using Attributary.Artifacts;

namespace Attributary.Output.Html;

public sealed class HtmlComplianceReportWriter : IComplianceReportWriter
{
    public OutputFormat Format => OutputFormat.Html;

    public string Render(ComplianceReportDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<h1>Compliance Report</h1>");
        sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Source</th></tr>");
        foreach (var entry in document.Entries)
            sb.AppendLine($"<tr><td>{Encode(entry.ComponentName)}</td><td>{Encode(entry.ComponentVersion)}</td>"
                + $"<td>{Encode(entry.LicenseId)}</td><td>{Encode(entry.LicenseTextSource?.ToString() ?? "unresolved")}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Flagged for review</h2>");
        sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Policy</th><th>Flags</th></tr>");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"<tr><td>{Encode(flag.ComponentName)}</td><td>{Encode(flag.ComponentVersion)}</td>"
                + $"<td>{Encode(flag.LicenseId)}</td><td>{Encode(flag.Policy.ToString())}</td><td>{Encode(string.Join(", ", flag.Flags))}</td></tr>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Output tests/Attributary.Output.Tests
git commit -m "feat(output): add HTML writers with encoded content and license linking"
```

---

## Task 20: Generate orchestrator — end-to-end pipeline composition

**Files:**
- Create: `src/Attributary.Rules/RuleEngineDiagnostics.cs`
- Create: `src/Attributary.Cli/Pipeline/GenerateOutput.cs`
- Create: `src/Attributary.Cli/Pipeline/GenerateOrchestrator.cs`
- Test: `tests/Attributary.Cli.Tests/Pipeline/GenerateOrchestratorTests.cs`

**Interfaces:**
- Consumes: `CycloneDxIngestor` (Task 4); `ILicenseResolutionChain` (Task 10); `IObligationPlanBuilder`, `RuleSet`, `LicensePolicy`, `ObligationKind` (Task 6); `IDiagnosticSink` (Task 3); `LicenseTextsDocumentBuilder`, `NoticeDocumentBuilder`, `AttributionDocumentBuilder`, `ComplianceReportDocumentBuilder` and their document types (Tasks 13-15).
- Produces:
  - `static class RuleEngineDiagnostics` with `PolicyDenied` (`ATT3001`, Error), `PolicyWarn` (`ATT3002`, Warning), `RequiredObligationUnresolved` (`ATT3010`, Error).
  - `record GenerateOutput(IReadOnlyList<ObligationPlan> Plans, LicenseTextsDocument LicenseTexts, NoticeDocument Notice, AttributionDocument Attribution, ComplianceReportDocument Report)`
  - `class GenerateOrchestrator(CycloneDxIngestor ingestor, ILicenseResolutionChain resolutionChain, IObligationPlanBuilder obligationPlanBuilder, IDiagnosticSink diagnostics)` with `Task<GenerateOutput> RunAsync(string sbomPath, RuleSet ruleSet, bool groupByLicense, bool embedLicenseText, bool failFast, CancellationToken ct)`.

This is where the "required data obligation unresolved" check (`ATT3010`) actually happens — it needs the matched rule's obligations (from `ObligationPlanBuilder`) together with the resolved fields (from `LicenseResolution`), which only both exist here, one layer above either individual stage.

- [ ] **Step 1: Write the failing end-to-end test**

```csharp
// tests/Attributary.Cli.Tests/Pipeline/GenerateOrchestratorTests.cs
using Attributary.Diagnostics;
using Attributary.Resolution;
using Attributary.Resolution.Sources;
using Attributary.Rules;
using Attributary.Sbom;

namespace Attributary.Cli.Tests.Pipeline;

public class GenerateOrchestratorTests
{
    private static string WriteFixtureSbom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Foo", "version": "1.0.0",
                  "copyright": "Copyright (c) Foo Inc.",
                  "licenses": [ { "license": { "id": "MIT" } } ] }
              ]
            }
            """);
        return path;
    }

    [Test]
    public async Task RunAsync_MitComponentWithEmbeddedCopyright_ProducesCompleteDocumentsAndNoDiagnostics()
    {
        var sbomPath = WriteFixtureSbom();
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(sbomPath, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(output.LicenseTexts.Licenses).HasCount().EqualTo(1);
        await Assert.That(output.Attribution.Rows[0].Copyright).IsEqualTo("Copyright (c) Foo Inc.");
        await Assert.That(output.Report.FlaggedForReview).IsEmpty();
        await Assert.That(diagnostics.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task RunAsync_GplComponent_ReportsPolicyWarnDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Bar", "version": "2.0.0",
                  "copyright": "Copyright (c) Bar Inc.",
                  "licenses": [ { "license": { "id": "GPL-3.0-only" } } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        await orchestrator.RunAsync(path, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3002");
    }

    [Test]
    public async Task RunAsync_LicenseTextObligationUnresolved_ReportsRequiredObligationDiagnostic()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Baz", "version": "3.0.0",
                  "copyright": "Copyright (c) Baz Inc.",
                  "licenses": [ { "license": { "id": "MIT" } } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([]); // no sources at all -> license text never resolves
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        await orchestrator.RunAsync(path, ruleSet, groupByLicense: true, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3010");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: FAIL — `GenerateOrchestrator`/`RuleEngineDiagnostics` do not exist.

- [ ] **Step 3: Implement**

```csharp
// src/Attributary.Rules/RuleEngineDiagnostics.cs
using Attributary.Diagnostics;

namespace Attributary.Rules;

public static class RuleEngineDiagnostics
{
    public static readonly DiagnosticDescriptor PolicyDenied =
        new("ATT3001", DiagnosticSeverity.Error, "License policy denied");

    public static readonly DiagnosticDescriptor PolicyWarn =
        new("ATT3002", DiagnosticSeverity.Warning, "License policy requires review");

    public static readonly DiagnosticDescriptor RequiredObligationUnresolved =
        new("ATT3010", DiagnosticSeverity.Error, "Required data obligation unresolved");
}
```

```csharp
// src/Attributary.Cli/Pipeline/GenerateOutput.cs
using Attributary.Artifacts;
using Attributary.Rules;

namespace Attributary.Cli.Pipeline;

public sealed record GenerateOutput(
    IReadOnlyList<ObligationPlan> Plans,
    LicenseTextsDocument LicenseTexts,
    NoticeDocument Notice,
    AttributionDocument Attribution,
    ComplianceReportDocument Report);
```

```csharp
// src/Attributary.Cli/Pipeline/GenerateOrchestrator.cs
using Attributary.Artifacts;
using Attributary.Diagnostics;
using Attributary.Resolution;
using Attributary.Rules;
using Attributary.Sbom;

namespace Attributary.Cli.Pipeline;

public sealed class GenerateOrchestrator(
    CycloneDxIngestor ingestor,
    ILicenseResolutionChain resolutionChain,
    IObligationPlanBuilder obligationPlanBuilder,
    IDiagnosticSink diagnostics)
{
    public async Task<GenerateOutput> RunAsync(
        string sbomPath,
        RuleSet ruleSet,
        bool groupByLicense,
        bool embedLicenseText,
        bool failFast,
        CancellationToken ct)
    {
        var components = ingestor.Ingest(sbomPath);
        var plans = new List<ObligationPlan>();

        foreach (var component in components)
        {
            var resolution = await resolutionChain.ResolveAsync(component, diagnostics, ct);
            var conditionResults = new Dictionary<string, bool>
            {
                ["upstream-notice-present"] = resolution.NoticeText is not null
            };
            var plan = obligationPlanBuilder.Build(resolution, ruleSet, conditionResults);
            plans.Add(plan);

            var context = $"{component.Name} {component.Version}";
            ReportPolicyDiagnostic(plan, context);
            ReportUnresolvedObligations(plan, context);

            if (failFast && diagnostics.HasErrors)
                break;
        }

        return new GenerateOutput(
            plans,
            LicenseTextsDocumentBuilder.Build(plans),
            NoticeDocumentBuilder.Build(plans),
            AttributionDocumentBuilder.Build(plans, groupByLicense, embedLicenseText),
            ComplianceReportDocumentBuilder.Build(plans));
    }

    private void ReportPolicyDiagnostic(ObligationPlan plan, string context)
    {
        var licenseId = plan.Resolution.ResolvedLicenseId ?? "UNKNOWN";
        if (plan.Policy == LicensePolicy.Deny)
            diagnostics.Report(RuleEngineDiagnostics.PolicyDenied, $"License '{licenseId}' is denied by policy.", context);
        else if (plan.Policy == LicensePolicy.Warn)
            diagnostics.Report(RuleEngineDiagnostics.PolicyWarn, $"License '{licenseId}' requires manual review.", context);
    }

    private void ReportUnresolvedObligations(ObligationPlan plan, string context)
    {
        foreach (var obligation in plan.Obligations)
        {
            var resolved = obligation.Kind switch
            {
                ObligationKind.Copyright => plan.Resolution.CopyrightText is not null,
                ObligationKind.LicenseText => plan.Resolution.LicenseText is not null,
                ObligationKind.NoticeText => plan.Resolution.NoticeText is not null,
                _ => true
            };

            if (!resolved)
                diagnostics.Report(RuleEngineDiagnostics.RequiredObligationUnresolved,
                    $"Required obligation '{obligation.Kind}' could not be resolved for license '{plan.Resolution.ResolvedLicenseId}'.", context);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Rules src/Attributary.Cli tests/Attributary.Cli.Tests
git commit -m "feat(cli): compose the end-to-end generate pipeline in GenerateOrchestrator"
```

---

## Task 21: `generate` command — composition root, flags, and file output

**Files:**
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj` (add `Spectre.Console`, `Spectre.Console.Cli`, `Microsoft.Extensions.DependencyInjection` package references)
- Create: `src/Attributary.Cli/SeverityOverridesParser.cs`
- Create: `src/Attributary.Rules/RuleSetMerger.cs`
- Create: `src/Attributary.Cli/Pipeline/GenerateCliOptions.cs`
- Create: `src/Attributary.Cli/Pipeline/GenerateRunner.cs`
- Create: `src/Attributary.Cli/Commands/GenerateCommandSettings.cs`
- Create: `src/Attributary.Cli/Commands/GenerateCommand.cs`
- Create: `src/Attributary.Cli/TypeRegistrar.cs`
- Create: `src/Attributary.Cli/TypeResolver.cs`
- Create: `src/Attributary.Cli/Program.cs`
- Test: `tests/Attributary.Cli.Tests/SeverityOverridesParserTests.cs`
- Test: `tests/Attributary.Rules.Tests/RuleSetMergerTests.cs`
- Test: `tests/Attributary.Cli.Tests/Pipeline/GenerateRunnerTests.cs`

**Interfaces:**
- Consumes: `GenerateOrchestrator`, `GenerateOutput` (Task 20); `RuleSet`, `IRuleSetLoader`, `YamlRuleSetLoader`, `IDefaultRuleSetProvider`, `DefaultRuleSetProvider` (Tasks 5-7); `ILicenseSource` implementations (Tasks 8-9); `CachingLicenseSource`, `FileSystemLicenseCacheStore` (Tasks 11-12); all writers and `OutputFormat` (Tasks 16-19); `SeverityOverrides`, `DiagnosticSink`, `MsBuildStyleDiagnosticFormatter`, `DiagnosticSeverity` (Tasks 2-3).
- Produces:
  - `static class SeverityOverridesParser { static SeverityOverrides Parse(bool warnAsErrorAll, string? warnAsErrorCodes, string? warnAsErrorExempt, string? noWarnCodes, string? severityPairs); }` — parses `CODE,CODE` comma lists and `CODE=Level,CODE=Level` pairs.
  - `static class RuleSetMerger { static RuleSet Merge(RuleSet baseline, RuleSet? overrides); }` — override rules replace a baseline rule with the same `IdPattern`; baseline rules not present in the override pass through unchanged; the override's `UnknownLicenseDefault` always wins when an override is supplied (see note below).
  - `record GenerateCliOptions(string SbomPath, string OutDir, IReadOnlyList<OutputFormat> Formats, bool GroupByLicense, bool EmbedLicenseText, bool DryRun, bool FailFast, bool NoCache, string? CacheDir, string? ConfigPath, SeverityOverrides SeverityOverrides)`
  - `class GenerateRunner(IAnsiConsole console) { Task<int> RunAsync(GenerateCliOptions options, CancellationToken ct); }`
  - `class GenerateCommandSettings : CommandSettings` (Spectre option surface), `class GenerateCommand(GenerateRunner runner) : AsyncCommand<GenerateCommandSettings>`

**Note on flag spelling vs. the spec:** the spec (§8) writes `--warnaserror[:CODE,...]` with an optional colon-suffixed value. Spectre.Console.Cli option parsing for "a flag that's sometimes bare, sometimes takes a value" is version-sensitive; to keep this unambiguous, `--warnaserror` is a plain bool (promotes *all* warnings) and code-scoped variants get their own named options (`--warnaserror-codes`, `--warnaserror-exempt`, `--nowarn`, `--severity`) — identical semantics to the spec, different spelling for parsing reliability.

**Note on config merging:** a `--config` file is loaded with the same `YamlRuleSetLoader` as the bundled defaults, so it must include its own `defaults.unknownLicense` section — `RuleSetMerger` doesn't try to detect "was defaults omitted," it just always takes the override's default when a config is supplied. `attributary init` (Task 24) scaffolds a config with `defaults.unknownLicense` already filled in from the bundled rules, so this is never a blank trap in practice.

- [ ] **Step 1: Add packages and write the failing tests for the two pure helpers**

```bash
dotnet add src/Attributary.Cli package Spectre.Console
dotnet add src/Attributary.Cli package Spectre.Console.Cli
dotnet add src/Attributary.Cli package Microsoft.Extensions.DependencyInjection
```

```csharp
// tests/Attributary.Cli.Tests/SeverityOverridesParserTests.cs
using Attributary.Diagnostics;

namespace Attributary.Cli.Tests;

public class SeverityOverridesParserTests
{
    [Test]
    public async Task Parse_CommaListsAndPairs_ProducesExpectedSets()
    {
        var overrides = SeverityOverridesParser.Parse(
            warnAsErrorAll: true,
            warnAsErrorCodes: null,
            warnAsErrorExempt: "ATT2500",
            noWarnCodes: "ATT0002, ATT1001",
            severityPairs: "ATT3001=warning");

        await Assert.That(overrides.WarnAsErrorAll).IsTrue();
        await Assert.That(overrides.WarnAsErrorExemptCodes).Contains("ATT2500");
        await Assert.That(overrides.NoWarnCodes).Contains("ATT0002").And.Contains("ATT1001");
        await Assert.That(overrides.ExplicitSeverities["ATT3001"]).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Parse_AllNull_ReturnsEmptySets()
    {
        var overrides = SeverityOverridesParser.Parse(false, null, null, null, null);

        await Assert.That(overrides.WarnAsErrorAll).IsFalse();
        await Assert.That(overrides.WarnAsErrorCodes).IsEmpty();
        await Assert.That(overrides.ExplicitSeverities).IsEmpty();
    }
}
```

```csharp
// tests/Attributary.Rules.Tests/RuleSetMergerTests.cs
namespace Attributary.Rules.Tests;

public class RuleSetMergerTests
{
    private static LicenseRule Rule(string id, LicensePolicy policy) => new(id, policy, [new Obligation(ObligationKind.LicenseText, null)], []);

    [Test]
    public async Task Merge_OverrideRule_ReplacesBaselineRuleWithSameId()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow)]);
        var overrides = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Warn)]);

        var merged = RuleSetMerger.Merge(baseline, overrides);

        await Assert.That(merged.Rules.Single(r => r.IdPattern == "MIT").Policy).IsEqualTo(LicensePolicy.Warn);
    }

    [Test]
    public async Task Merge_BaselineRuleNotInOverride_PassesThroughUnchanged()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow), Rule("Apache-2.0", LicensePolicy.Allow)]);
        var overrides = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Warn)]);

        var merged = RuleSetMerger.Merge(baseline, overrides);

        await Assert.That(merged.Rules.Select(r => r.IdPattern)).Contains("Apache-2.0");
    }

    [Test]
    public async Task Merge_NullOverride_ReturnsBaselineUnchanged()
    {
        var baseline = new RuleSet(Rule("*", LicensePolicy.Deny), [Rule("MIT", LicensePolicy.Allow)]);

        var merged = RuleSetMerger.Merge(baseline, null);

        await Assert.That(merged).IsEqualTo(baseline);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Cli.Tests tests/Attributary.Rules.Tests`
Expected: FAIL — `SeverityOverridesParser`/`RuleSetMerger` do not exist.

- [ ] **Step 3: Implement the two helpers**

```csharp
// src/Attributary.Cli/SeverityOverridesParser.cs
using Attributary.Diagnostics;

namespace Attributary.Cli;

public static class SeverityOverridesParser
{
    public static SeverityOverrides Parse(
        bool warnAsErrorAll, string? warnAsErrorCodes, string? warnAsErrorExempt, string? noWarnCodes, string? severityPairs) => new(
        WarnAsErrorAll: warnAsErrorAll,
        WarnAsErrorCodes: ParseCodeList(warnAsErrorCodes),
        WarnAsErrorExemptCodes: ParseCodeList(warnAsErrorExempt),
        NoWarnCodes: ParseCodeList(noWarnCodes),
        ExplicitSeverities: ParseSeverityPairs(severityPairs));

    private static IReadOnlySet<string> ParseCodeList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>()
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    private static IReadOnlyDictionary<string, DiagnosticSeverity> ParseSeverityPairs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new Dictionary<string, DiagnosticSeverity>();

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Enum.Parse<DiagnosticSeverity>(parts[1], ignoreCase: true));
    }
}
```

```csharp
// src/Attributary.Rules/RuleSetMerger.cs
namespace Attributary.Rules;

public static class RuleSetMerger
{
    public static RuleSet Merge(RuleSet baseline, RuleSet? overrides)
    {
        if (overrides is null)
            return baseline;

        var overrideIds = overrides.Rules.Select(r => r.IdPattern).ToHashSet();
        var mergedRules = overrides.Rules
            .Concat(baseline.Rules.Where(r => !overrideIds.Contains(r.IdPattern)))
            .ToList();

        return new RuleSet(overrides.UnknownLicenseDefault, mergedRules);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Cli.Tests tests/Attributary.Rules.Tests`
Expected: PASS — all 5 tests green.

- [ ] **Step 5: Write the failing test for `GenerateRunner`**

```csharp
// tests/Attributary.Cli.Tests/Pipeline/GenerateRunnerTests.cs
using Attributary.Diagnostics;
using Spectre.Console;

namespace Attributary.Cli.Tests.Pipeline;

public class GenerateRunnerTests
{
    private static string WriteFixtureSbom(string licenseId)
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, $$"""
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Foo", "version": "1.0.0",
                  "copyright": "Copyright (c) Foo Inc.",
                  "licenses": [ { "license": { "id": "{{licenseId}}" } } ] }
              ]
            }
            """);
        return path;
    }

    [Test]
    public async Task RunAsync_MitComponent_WritesLicenseAttributionAndReportFiles()
    {
        var sbomPath = WriteFixtureSbom("MIT");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.Exists(Path.Combine(outDir, "LICENSES", "MIT.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outDir, "THIRD-PARTY-NOTICES.txt"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(outDir, "COMPLIANCE-REPORT.txt"))).IsTrue();
    }

    [Test]
    public async Task RunAsync_DenyPolicyLicense_ReturnsNonZeroExitCode()
    {
        var sbomPath = WriteFixtureSbom("JSON");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: false, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    [Test]
    public async Task RunAsync_DryRun_DoesNotCreateOutputDirectory()
    {
        var sbomPath = WriteFixtureSbom("MIT");
        var outDir = Path.Combine(Path.GetTempPath(), $"attributary-out-{Guid.NewGuid()}");
        var options = new GenerateCliOptions(
            sbomPath, outDir, [OutputFormat.Txt], GroupByLicense: true, EmbedLicenseText: true,
            DryRun: true, FailFast: false, NoCache: true, CacheDir: null, ConfigPath: null,
            SeverityOverridesParser.Parse(false, null, null, null, null));
        var runner = new GenerateRunner(AnsiConsole.Console);

        await runner.RunAsync(options, CancellationToken.None);

        await Assert.That(Directory.Exists(outDir)).IsFalse();
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: FAIL — `GenerateCliOptions`/`GenerateRunner`/`OutputFormat` (unqualified) do not resolve.

- [ ] **Step 7: Implement `GenerateCliOptions`, `GenerateRunner`, the Spectre command, and the composition root**

```csharp
// src/Attributary.Cli/Pipeline/GenerateCliOptions.cs
using Attributary.Diagnostics;
using Attributary.Output;

namespace Attributary.Cli.Pipeline;

public sealed record GenerateCliOptions(
    string SbomPath,
    string OutDir,
    IReadOnlyList<OutputFormat> Formats,
    bool GroupByLicense,
    bool EmbedLicenseText,
    bool DryRun,
    bool FailFast,
    bool NoCache,
    string? CacheDir,
    string? ConfigPath,
    SeverityOverrides SeverityOverrides);
```

```csharp
// src/Attributary.Cli/Pipeline/GenerateRunner.cs
using Attributary.Diagnostics;
using Attributary.Output;
using Attributary.Output.Html;
using Attributary.Output.Json;
using Attributary.Output.Markdown;
using Attributary.Output.Text;
using Attributary.Resolution;
using Attributary.Resolution.Caching;
using Attributary.Resolution.Sources;
using Attributary.Rules;
using Attributary.Sbom;
using Spectre.Console;

namespace Attributary.Cli.Pipeline;

public sealed class GenerateRunner(IAnsiConsole console)
{
    private static readonly IReadOnlyDictionary<OutputFormat, IAttributionWriter> AttributionWriters = new Dictionary<OutputFormat, IAttributionWriter>
    {
        [OutputFormat.Txt] = new TxtAttributionWriter(),
        [OutputFormat.Md] = new MdAttributionWriter(),
        [OutputFormat.Json] = new JsonAttributionWriter(),
        [OutputFormat.Html] = new HtmlAttributionWriter()
    };

    private static readonly IReadOnlyDictionary<OutputFormat, IComplianceReportWriter> ReportWriters = new Dictionary<OutputFormat, IComplianceReportWriter>
    {
        [OutputFormat.Txt] = new TxtComplianceReportWriter(),
        [OutputFormat.Md] = new MdComplianceReportWriter(),
        [OutputFormat.Json] = new JsonComplianceReportWriter(),
        [OutputFormat.Html] = new HtmlComplianceReportWriter()
    };

    public async Task<int> RunAsync(GenerateCliOptions options, CancellationToken ct)
    {
        var diagnostics = new DiagnosticSink(options.SeverityOverrides);
        var ruleSet = LoadRuleSet(options.ConfigPath);
        var chain = new LicenseResolutionChain(BuildSources(options));
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);

        var output = await orchestrator.RunAsync(options.SbomPath, ruleSet, options.GroupByLicense, options.EmbedLicenseText, options.FailFast, ct);

        if (!options.DryRun)
            WriteFiles(options, output);

        PrintDiagnostics(diagnostics);
        return diagnostics.HasErrors ? 1 : 0;
    }

    private static RuleSet LoadRuleSet(string? configPath)
    {
        var baseline = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();
        if (configPath is null || !File.Exists(configPath))
            return baseline;

        var overrideRuleSet = new YamlRuleSetLoader().Load(File.ReadAllText(configPath));
        return RuleSetMerger.Merge(baseline, overrideRuleSet);
    }

    private static IReadOnlyList<ILicenseSource> BuildSources(GenerateCliOptions options)
    {
        var cacheDir = options.CacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Attributary", "cache");
        var store = new FileSystemLicenseCacheStore(cacheDir);

        ILicenseSource WithCache(ILicenseSource source) => options.NoCache ? source : new CachingLicenseSource(source, store);

        var globalPackagesFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        return
        [
            new SbomEmbeddedSource(),
            WithCache(new NuGetLocalCacheSource(globalPackagesFolder)),
            WithCache(new GitHubVcsSource(new HttpClient())),
            WithCache(new SpdxCanonicalSource())
        ];
    }

    private static void WriteFiles(GenerateCliOptions options, GenerateOutput output)
    {
        Directory.CreateDirectory(options.OutDir);

        var licensesDir = Path.Combine(options.OutDir, "LICENSES");
        Directory.CreateDirectory(licensesDir);
        foreach (var entry in output.LicenseTexts.Licenses)
            File.WriteAllText(Path.Combine(licensesDir, $"{entry.LicenseId}.txt"), entry.Text);

        if (output.Notice.Sections.Count > 0)
            File.WriteAllText(Path.Combine(options.OutDir, "NOTICE.txt"), new PlainTextNoticeWriter().Render(output.Notice));

        foreach (var format in options.Formats)
        {
            var extension = format.ToString().ToLowerInvariant();
            File.WriteAllText(Path.Combine(options.OutDir, $"THIRD-PARTY-NOTICES.{extension}"), AttributionWriters[format].Render(output.Attribution));
            File.WriteAllText(Path.Combine(options.OutDir, $"COMPLIANCE-REPORT.{extension}"), ReportWriters[format].Render(output.Report));
        }
    }

    private void PrintDiagnostics(IDiagnosticSink diagnostics)
    {
        var formatter = new MsBuildStyleDiagnosticFormatter();
        foreach (var diagnostic in diagnostics.Diagnostics)
            console.WriteLine(formatter.Format(diagnostic));

        var errorCount = diagnostics.Diagnostics.Count(d => d.EffectiveSeverity == DiagnosticSeverity.Error);
        var warningCount = diagnostics.Diagnostics.Count(d => d.EffectiveSeverity == DiagnosticSeverity.Warning);
        console.WriteLine($"{errorCount} error(s), {warningCount} warning(s)");
    }
}
```

```csharp
// src/Attributary.Cli/Commands/GenerateCommandSettings.cs
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class GenerateCommandSettings : CommandSettings
{
    [CommandOption("--sbom <PATH>")]
    public required string SbomPath { get; init; }

    [CommandOption("--out <DIR>")]
    public string OutDir { get; init; } = "./compliance";

    [CommandOption("--format <FORMATS>")]
    public string Format { get; init; } = "txt";

    [CommandOption("--group-by <MODE>")]
    public string GroupBy { get; init; } = "license";

    [CommandOption("--no-embed-license-text")]
    public bool NoEmbedLicenseText { get; init; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [CommandOption("--fail-fast")]
    public bool FailFast { get; init; }

    [CommandOption("--no-cache")]
    public bool NoCache { get; init; }

    [CommandOption("--cache-dir <PATH>")]
    public string? CacheDir { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }

    [CommandOption("--warnaserror")]
    public bool WarnAsErrorAll { get; init; }

    [CommandOption("--warnaserror-codes <CODES>")]
    public string? WarnAsErrorCodes { get; init; }

    [CommandOption("--warnaserror-exempt <CODES>")]
    public string? WarnAsErrorExempt { get; init; }

    [CommandOption("--nowarn <CODES>")]
    public string? NoWarnCodes { get; init; }

    [CommandOption("--severity <PAIRS>")]
    public string? Severity { get; init; }
}
```

```csharp
// src/Attributary.Cli/Commands/GenerateCommand.cs
using Attributary.Cli.Pipeline;
using Attributary.Output;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class GenerateCommand(GenerateRunner runner) : AsyncCommand<GenerateCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, GenerateCommandSettings settings)
    {
        var formats = settings.Format
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(f => Enum.Parse<OutputFormat>(f, ignoreCase: true))
            .ToList();

        var options = new GenerateCliOptions(
            settings.SbomPath,
            settings.OutDir,
            formats,
            GroupByLicense: settings.GroupBy.Equals("license", StringComparison.OrdinalIgnoreCase),
            EmbedLicenseText: !settings.NoEmbedLicenseText,
            settings.DryRun,
            settings.FailFast,
            settings.NoCache,
            settings.CacheDir,
            settings.ConfigPath,
            SeverityOverridesParser.Parse(settings.WarnAsErrorAll, settings.WarnAsErrorCodes, settings.WarnAsErrorExempt, settings.NoWarnCodes, settings.Severity));

        return await runner.RunAsync(options, CancellationToken.None);
    }
}
```

```csharp
// src/Attributary.Cli/TypeRegistrar.cs
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Attributary.Cli;

public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());
    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);
    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);
    public void RegisterLazy(Type service, Func<object> factory) => services.AddSingleton(service, _ => factory());
}
```

```csharp
// src/Attributary.Cli/TypeResolver.cs
using Spectre.Console.Cli;

namespace Attributary.Cli;

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);

    public void Dispose()
    {
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }
}
```

```csharp
// src/Attributary.Cli/Program.cs
using Attributary.Cli;
using Attributary.Cli.Commands;
using Attributary.Cli.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddSingleton(AnsiConsole.Console);
services.AddSingleton<GenerateRunner>();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);
app.Configure(config =>
{
    config.AddCommand<GenerateCommand>("generate");
});

return app.Run(args);
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: PASS — all 3 `GenerateRunner` tests green.

- [ ] **Step 9: Run the full solution build and test suite**

Run: `dotnet build Attributary.sln && dotnet test Attributary.sln`
Expected: Build succeeds; all tests across every project pass.

- [ ] **Step 10: Commit**

```bash
git add src/Attributary.Cli src/Attributary.Rules tests/Attributary.Cli.Tests tests/Attributary.Rules.Tests
git commit -m "feat(cli): wire the generate command with full flag surface and DI composition root"
```

---

## Task 22: `license list` / `license show` commands

**Files:**
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj` (no new package needed — Spectre already referenced)
- Modify: `tests/Attributary.Cli.Tests/Attributary.Cli.Tests.csproj` (add `Spectre.Console.Testing`)
- Modify: `src/Attributary.Cli/Pipeline/GenerateRunner.cs` (use the new shared `RuleSetConfigLoader` instead of its own inline loader)
- Create: `src/Attributary.Rules/RuleSetConfigLoader.cs`
- Create: `src/Attributary.Cli/Commands/LicenseListCommandSettings.cs`
- Create: `src/Attributary.Cli/Commands/LicenseListCommand.cs`
- Create: `src/Attributary.Cli/Commands/LicenseShowCommandSettings.cs`
- Create: `src/Attributary.Cli/Commands/LicenseShowCommand.cs`
- Modify: `src/Attributary.Cli/Program.cs` (add the `license` branch)
- Test: `tests/Attributary.Cli.Tests/Commands/LicenseListCommandTests.cs`
- Test: `tests/Attributary.Cli.Tests/Commands/LicenseShowCommandTests.cs`

**Interfaces:**
- Consumes: `IDefaultRuleSetProvider`, `DefaultRuleSetProvider`, `YamlRuleSetLoader`, `RuleSetMerger`, `RuleSet`, `IRuleMatcher`, `RuleMatcher`, `LicenseRule` (Tasks 5-7, 21); `CycloneDxIngestor`, `SbomComponent` (Task 4).
- Produces:
  - `static class RuleSetConfigLoader { static RuleSet LoadMerged(string? configPath, IDefaultRuleSetProvider defaultProvider, IRuleSetLoader loader); }`
  - `class LicenseListCommandSettings : CommandSettings` (`--sbom`, `--config`), `class LicenseListCommand(IAnsiConsole console) : Command<LicenseListCommandSettings>`
  - `class LicenseShowCommandSettings : CommandSettings` (positional `LICENSE_ID`, `--config`), `class LicenseShowCommand(IAnsiConsole console) : Command<LicenseShowCommandSettings>`

- [ ] **Step 1: Add the testing package and write the failing tests**

```bash
dotnet add tests/Attributary.Cli.Tests package Spectre.Console.Testing
```

```csharp
// tests/Attributary.Cli.Tests/Commands/LicenseListCommandTests.cs
using Attributary.Cli.Commands;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class LicenseListCommandTests
{
    [Test]
    public async Task Execute_MitComponent_ListsAllowPolicy()
    {
        var sbomPath = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(sbomPath, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [ { "type": "library", "name": "Foo", "version": "1.0.0", "licenses": [ { "license": { "id": "MIT" } } ] } ]
            }
            """);
        var console = new TestConsole();
        var command = new LicenseListCommand(console);

        var exitCode = command.Execute(null!, new LicenseListCommandSettings { SbomPath = sbomPath, ConfigPath = null });

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("MIT").And.Contains("Allow");
    }
}
```

```csharp
// tests/Attributary.Cli.Tests/Commands/LicenseShowCommandTests.cs
using Attributary.Cli.Commands;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class LicenseShowCommandTests
{
    [Test]
    public async Task Execute_Apache2_ShowsPolicyAndConditionalNoticeObligation()
    {
        var console = new TestConsole();
        var command = new LicenseShowCommand(console);

        var exitCode = command.Execute(null!, new LicenseShowCommandSettings { LicenseId = "Apache-2.0", ConfigPath = null });

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Allow");
        await Assert.That(console.Output).Contains("NoticeText");
        await Assert.That(console.Output).Contains("upstream-notice-present");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: FAIL — `LicenseListCommand`/`LicenseShowCommand`/`RuleSetConfigLoader` do not exist.

- [ ] **Step 3: Implement the shared loader and both commands**

```csharp
// src/Attributary.Rules/RuleSetConfigLoader.cs
namespace Attributary.Rules;

public static class RuleSetConfigLoader
{
    public static RuleSet LoadMerged(string? configPath, IDefaultRuleSetProvider defaultProvider, IRuleSetLoader loader)
    {
        var baseline = defaultProvider.Load();
        if (configPath is null || !File.Exists(configPath))
            return baseline;

        var overrideRuleSet = loader.Load(File.ReadAllText(configPath));
        return RuleSetMerger.Merge(baseline, overrideRuleSet);
    }
}
```

```csharp
// src/Attributary.Cli/Commands/LicenseListCommandSettings.cs
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseListCommandSettings : CommandSettings
{
    [CommandOption("--sbom <PATH>")]
    public required string SbomPath { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }
}
```

```csharp
// src/Attributary.Cli/Commands/LicenseListCommand.cs
using Attributary.Rules;
using Attributary.Sbom;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseListCommand(IAnsiConsole console) : Command<LicenseListCommandSettings>
{
    public override int Execute(CommandContext context, LicenseListCommandSettings settings)
    {
        var components = new CycloneDxIngestor().Ingest(settings.SbomPath);
        var bundled = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();
        var merged = RuleSetConfigLoader.LoadMerged(settings.ConfigPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());
        var matcher = new RuleMatcher();

        var table = new Table();
        table.AddColumn("Component");
        table.AddColumn("License");
        table.AddColumn("Policy");
        table.AddColumn("Custom rule");

        foreach (var component in components)
        {
            var licenseId = component.DeclaredLicense.SpdxId
                ?? component.DeclaredLicense.FreeTextName
                ?? component.DeclaredLicense.SpdxExpression
                ?? "UNKNOWN";
            var mergedRule = matcher.Match(licenseId, merged);
            var bundledRule = matcher.Match(licenseId, bundled);

            table.AddRow(
                $"{component.Name} {component.Version}",
                licenseId,
                mergedRule.Policy.ToString(),
                (mergedRule != bundledRule).ToString());
        }

        console.Write(table);
        return 0;
    }
}
```

```csharp
// src/Attributary.Cli/Commands/LicenseShowCommandSettings.cs
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseShowCommandSettings : CommandSettings
{
    [CommandArgument(0, "<LICENSE_ID>")]
    public required string LicenseId { get; init; }

    [CommandOption("--config <PATH>")]
    public string? ConfigPath { get; init; }
}
```

```csharp
// src/Attributary.Cli/Commands/LicenseShowCommand.cs
using Attributary.Rules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class LicenseShowCommand(IAnsiConsole console) : Command<LicenseShowCommandSettings>
{
    public override int Execute(CommandContext context, LicenseShowCommandSettings settings)
    {
        var ruleSet = RuleSetConfigLoader.LoadMerged(settings.ConfigPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());
        var rule = new RuleMatcher().Match(settings.LicenseId, ruleSet);

        console.WriteLine($"License: {settings.LicenseId}");
        console.WriteLine($"Policy: {rule.Policy}");
        console.WriteLine("Requires:");
        foreach (var obligation in rule.Require)
            console.WriteLine($"  - {obligation.Kind}" + (obligation.Condition is null ? "" : $" (when: {obligation.Condition})"));
        console.WriteLine("Flags:");
        foreach (var flag in rule.Flags)
            console.WriteLine($"  - {flag}");

        return 0;
    }
}
```

- [ ] **Step 4: Update `GenerateRunner.LoadRuleSet` to use the shared loader**

```csharp
// src/Attributary.Cli/Pipeline/GenerateRunner.cs — replace the existing LoadRuleSet method
private static RuleSet LoadRuleSet(string? configPath) =>
    RuleSetConfigLoader.LoadMerged(configPath, new DefaultRuleSetProvider(new YamlRuleSetLoader()), new YamlRuleSetLoader());
```

- [ ] **Step 5: Register the `license` branch in `Program.cs`**

```csharp
// src/Attributary.Cli/Program.cs — replace the app.Configure block
app.Configure(config =>
{
    config.AddCommand<GenerateCommand>("generate");
    config.AddBranch("license", license =>
    {
        license.AddCommand<LicenseListCommand>("list");
        license.AddCommand<LicenseShowCommand>("show");
    });
});
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: PASS — all tests green, including the earlier `GenerateRunner` tests (unaffected by the refactor).

- [ ] **Step 7: Commit**

```bash
git add src/Attributary.Rules src/Attributary.Cli tests/Attributary.Cli.Tests
git commit -m "feat(cli): add license list/show commands and share rule-set config loading"
```

---

## Task 23: `cache` commands and `init`

**Files:**
- Modify: `src/Attributary.Rules/IDefaultRuleSetProvider.cs` (add `string LoadRawYaml();`)
- Modify: `src/Attributary.Rules/DefaultRuleSetProvider.cs` (implement it)
- Create: `src/Attributary.Cli/CacheDirectoryResolver.cs`
- Modify: `src/Attributary.Cli/Pipeline/GenerateRunner.cs` (use `CacheDirectoryResolver` instead of its own inline default)
- Create: `src/Attributary.Cli/Commands/CacheDirCommandSettings.cs`
- Create: `src/Attributary.Cli/Commands/CacheClearCommand.cs`
- Create: `src/Attributary.Cli/Commands/CacheListCommand.cs`
- Create: `src/Attributary.Cli/Commands/CachePathCommand.cs`
- Create: `src/Attributary.Cli/Commands/InitCommandSettings.cs`
- Create: `src/Attributary.Cli/Commands/InitCommand.cs`
- Modify: `src/Attributary.Cli/Program.cs` (add the `cache` branch and `init` command)
- Test: `tests/Attributary.Rules.Tests/DefaultRuleSetProviderTests.cs` (add a `LoadRawYaml` test)
- Test: `tests/Attributary.Cli.Tests/Commands/CacheCommandsTests.cs`
- Test: `tests/Attributary.Cli.Tests/Commands/InitCommandTests.cs`

**Scope note:** the spec's config file also anticipates `diagnostics`, `output`, and `cache` sections (§9, §11). This plan ships `rules`-section config loading (Tasks 21-22) and CLI flags for everything else — `init` scaffolds a valid `defaults`+`rules` config (exactly what `RuleSetConfigLoader` consumes today). Config-file-driven `diagnostics`/`output`/`cache` sections are a v2 follow-up, not a v1 requirement; CLI flags already cover the same ground for a single run.

- [ ] **Step 1: Add `LoadRawYaml` to the default rule set provider and write its failing test**

```csharp
// tests/Attributary.Rules.Tests/DefaultRuleSetProviderTests.cs — add this test to the existing class
[Test]
public async Task LoadRawYaml_ContainsDefaultsAndRulesKeys()
{
    var provider = new DefaultRuleSetProvider(new YamlRuleSetLoader());

    var yaml = provider.LoadRawYaml();

    await Assert.That(yaml).Contains("defaults:").And.Contains("rules:").And.Contains("MIT");
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: FAIL — `IDefaultRuleSetProvider` has no `LoadRawYaml` member.

- [ ] **Step 3: Implement `LoadRawYaml`**

```csharp
// src/Attributary.Rules/IDefaultRuleSetProvider.cs
namespace Attributary.Rules;

public interface IDefaultRuleSetProvider
{
    RuleSet Load();
    string LoadRawYaml();
}
```

```csharp
// src/Attributary.Rules/DefaultRuleSetProvider.cs — replace the class body
using System.Reflection;

namespace Attributary.Rules;

public sealed class DefaultRuleSetProvider(IRuleSetLoader loader) : IDefaultRuleSetProvider
{
    public RuleSet Load() => loader.Load(LoadRawYaml());

    public string LoadRawYaml()
    {
        var assembly = typeof(DefaultRuleSetProvider).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(n => n.EndsWith("DefaultRules.yaml"));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: PASS.

- [ ] **Step 5: Write the failing tests for the cache and init commands**

```csharp
// tests/Attributary.Cli.Tests/Commands/CacheCommandsTests.cs
using Attributary.Cli.Commands;
using Attributary.Resolution.Caching;
using Attributary.Domain;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class CacheCommandsTests
{
    [Test]
    public async Task CachePathCommand_NoOverride_PrintsOsStandardCacheDirectory()
    {
        var console = new TestConsole();
        var command = new CachePathCommand(console);

        var exitCode = command.Execute(null!, new CacheDirCommandSettings { CacheDir = null });

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Attributary");
    }

    [Test]
    public async Task CacheListCommand_ThenClearCommand_ListsThenEmpties()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), $"attributary-cache-{Guid.NewGuid()}");
        var store = new FileSystemLicenseCacheStore(cacheDir);
        store.Put(new CacheKey(ResolutionSourceStrategy.SpdxCanonical, "MIT"),
            new CacheEntry("text", FileSystemLicenseCacheStore.ComputeSha256("text"), null, DateTimeOffset.UtcNow));

        var listConsole = new TestConsole();
        new CacheListCommand(listConsole).Execute(null!, new CacheDirCommandSettings { CacheDir = cacheDir });
        await Assert.That(listConsole.Output).Contains("MIT");

        var clearConsole = new TestConsole();
        new CacheClearCommand(clearConsole).Execute(null!, new CacheDirCommandSettings { CacheDir = cacheDir });
        await Assert.That(store.List()).IsEmpty();
    }
}
```

```csharp
// tests/Attributary.Cli.Tests/Commands/InitCommandTests.cs
using Attributary.Cli.Commands;
using Spectre.Console.Testing;

namespace Attributary.Cli.Tests.Commands;

public class InitCommandTests
{
    [Test]
    public async Task Execute_NoExistingFile_WritesBundledDefaultsConfig()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"attributary-config-{Guid.NewGuid()}.yaml");
        var console = new TestConsole();
        var command = new InitCommand(console);

        var exitCode = command.Execute(null!, new InitCommandSettings { OutPath = outPath });

        await Assert.That(exitCode).IsEqualTo(0);
        await Assert.That(File.Exists(outPath)).IsTrue();
        await Assert.That(File.ReadAllText(outPath)).Contains("defaults:").And.Contains("MIT");
    }

    [Test]
    public async Task Execute_FileAlreadyExists_DoesNotOverwriteAndReturnsNonZero()
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"attributary-config-{Guid.NewGuid()}.yaml");
        File.WriteAllText(outPath, "existing content");
        var console = new TestConsole();
        var command = new InitCommand(console);

        var exitCode = command.Execute(null!, new InitCommandSettings { OutPath = outPath });

        await Assert.That(exitCode).IsEqualTo(1);
        await Assert.That(File.ReadAllText(outPath)).IsEqualTo("existing content");
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: FAIL — none of the command types exist.

- [ ] **Step 7: Implement**

```csharp
// src/Attributary.Cli/CacheDirectoryResolver.cs
namespace Attributary.Cli;

public static class CacheDirectoryResolver
{
    public static string Resolve(string? cacheDir) => cacheDir ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Attributary", "cache");
}
```

```csharp
// src/Attributary.Cli/Commands/CacheDirCommandSettings.cs
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheDirCommandSettings : CommandSettings
{
    [CommandOption("--cache-dir <PATH>")]
    public string? CacheDir { get; init; }
}
```

```csharp
// src/Attributary.Cli/Commands/CacheClearCommand.cs
using Attributary.Resolution.Caching;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheClearCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    public override int Execute(CommandContext context, CacheDirCommandSettings settings)
    {
        new FileSystemLicenseCacheStore(CacheDirectoryResolver.Resolve(settings.CacheDir)).Clear();
        console.WriteLine("Cache cleared.");
        return 0;
    }
}
```

```csharp
// src/Attributary.Cli/Commands/CacheListCommand.cs
using Attributary.Resolution.Caching;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CacheListCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    public override int Execute(CommandContext context, CacheDirCommandSettings settings)
    {
        var store = new FileSystemLicenseCacheStore(CacheDirectoryResolver.Resolve(settings.CacheDir));
        foreach (var key in store.List())
            console.WriteLine($"{key.Strategy}: {key.Discriminator}");
        return 0;
    }
}
```

```csharp
// src/Attributary.Cli/Commands/CachePathCommand.cs
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class CachePathCommand(IAnsiConsole console) : Command<CacheDirCommandSettings>
{
    public override int Execute(CommandContext context, CacheDirCommandSettings settings)
    {
        console.WriteLine(CacheDirectoryResolver.Resolve(settings.CacheDir));
        return 0;
    }
}
```

```csharp
// src/Attributary.Cli/Commands/InitCommandSettings.cs
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class InitCommandSettings : CommandSettings
{
    [CommandOption("--out <PATH>")]
    public string OutPath { get; init; } = "attributary.config.yaml";
}
```

```csharp
// src/Attributary.Cli/Commands/InitCommand.cs
using Attributary.Rules;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Attributary.Cli.Commands;

public sealed class InitCommand(IAnsiConsole console) : Command<InitCommandSettings>
{
    public override int Execute(CommandContext context, InitCommandSettings settings)
    {
        if (File.Exists(settings.OutPath))
        {
            console.WriteLine($"{settings.OutPath} already exists; not overwriting.");
            return 1;
        }

        var bundledYaml = new DefaultRuleSetProvider(new YamlRuleSetLoader()).LoadRawYaml();
        File.WriteAllText(settings.OutPath, bundledYaml);
        console.WriteLine($"Wrote starter config to {settings.OutPath}.");
        return 0;
    }
}
```

- [ ] **Step 8: Update `GenerateRunner.BuildSources` to use `CacheDirectoryResolver`**

```csharp
// src/Attributary.Cli/Pipeline/GenerateRunner.cs — replace the inline default inside BuildSources
var cacheDir = CacheDirectoryResolver.Resolve(options.CacheDir);
```

- [ ] **Step 9: Register the `cache` branch and `init` command in `Program.cs`**

```csharp
// src/Attributary.Cli/Program.cs — replace the app.Configure block
app.Configure(config =>
{
    config.AddCommand<GenerateCommand>("generate");
    config.AddBranch("license", license =>
    {
        license.AddCommand<LicenseListCommand>("list");
        license.AddCommand<LicenseShowCommand>("show");
    });
    config.AddBranch("cache", cache =>
    {
        cache.AddCommand<CacheClearCommand>("clear");
        cache.AddCommand<CacheListCommand>("list");
        cache.AddCommand<CachePathCommand>("path");
    });
    config.AddCommand<InitCommand>("init");
});
```

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test Attributary.sln`
Expected: PASS — the entire solution's test suite is green.

- [ ] **Step 11: Commit**

```bash
git add src/Attributary.Rules src/Attributary.Cli tests/Attributary.Rules.Tests tests/Attributary.Cli.Tests
git commit -m "feat(cli): add cache clear/list/path commands and init scaffolding"
```

---

## Task 24: Package as a .NET tool

**Files:**
- Modify: `src/Attributary.Cli/Attributary.Cli.csproj` (add `PackAsTool`, `ToolCommandName`, `PackageId`, `Version`)

**Interfaces:**
- Consumes: the finished CLI from Tasks 21-23.
- Produces: no new types — this task packages the existing `Attributary.Cli` executable as an installable .NET tool per spec §11.

This task has no unit test cycle (it's packaging configuration, not application logic); its "test" is the manual pack-and-install smoke check in Steps 2-4.

- [ ] **Step 1: Add the tool packaging properties**

```xml
<!-- src/Attributary.Cli/Attributary.Cli.csproj — add inside the existing <PropertyGroup> -->
<PackAsTool>true</PackAsTool>
<ToolCommandName>attributary</ToolCommandName>
<PackageId>Attributary</PackageId>
<Version>0.1.0</Version>
```

- [ ] **Step 2: Pack it**

```bash
dotnet pack src/Attributary.Cli -c Release -o ./nupkg
```

Expected: a `Attributary.0.1.0.nupkg` file appears in `./nupkg`.

- [ ] **Step 3: Install it as a local tool into a scratch directory and smoke-test it**

```bash
mkdir -p /tmp/attributary-tool-smoke-test && cd /tmp/attributary-tool-smoke-test
dotnet new tool-manifest
dotnet tool install --add-source /path/to/repo/nupkg --local Attributary
dotnet tool run attributary -- generate --help
```

Expected: help text for the `generate` command prints, listing `--sbom`, `--out`, `--format`, `--dry-run`, `--fail-fast`, etc.

- [ ] **Step 4: Uninstall the scratch install and clean up**

```bash
dotnet tool uninstall --local Attributary
cd - && rm -rf /tmp/attributary-tool-smoke-test
```

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Cli
git commit -m "chore(cli): package Attributary as a .NET tool"
```

---

## Known follow-ups (not covered by this plan)

- **ATT0xxx / ATT1xxx / ATT4xxx are reserved but unused, and `ATT2500` (cache integrity) is never raised.** The diagnostic numbering scheme (Global Constraints, spec §8) ranges codes by stage and specifically calls out `ATT2500` as a warning for a cache-integrity-check failure, but this plan only wires `ATT2001` (Task 10) and `ATT3001`/`ATT3002`/`ATT3010` (Task 20) through the diagnostic sink. A malformed SBOM file (`CycloneDxIngestor.Ingest`), a malformed or unknown-obligation config file (`YamlRuleSetLoader`), an unwritable output path (`GenerateRunner.WriteFiles`), and a tampered/corrupted cache entry (`FileSystemLicenseCacheStore.TryGet` in Task 11, which silently falls back to re-resolving on a hash mismatch) currently produce either a raw exception or no signal at all, instead of a formatted diagnostic. `IDiagnosticSink` and `MsBuildStyleDiagnosticFormatter` (Task 3) already exist and are directly reusable for all of these — it's a mechanical follow-up (wrap the relevant call sites, report a descriptor, keep going or fail per severity) once the core pipeline built here is stable, not a redesign.
- **Config-file `diagnostics`/`output`/`cache` sections** are deferred per Task 23's scope note — v1 covers the same ground via CLI flags only.
- Everything else listed in the spec's §12 "Deferred (v2+) ideas" remains deferred, unchanged.
