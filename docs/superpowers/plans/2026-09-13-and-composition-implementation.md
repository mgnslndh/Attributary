# Multi-license (AND) Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire real multi-license ("flat AND") support into Attributary's license resolution, rule engine, artifact generation, and output writers, per spec §6a.

**Architecture:** `LicenseResolution` moves from "one license id, one license text" to "a list of license ids, a dictionary of license text per id" — a genuine reshape, not an additive change. Every consumer across Resolution, Rules, Artifacts, Output, and Cli is updated in this same plan, since none of them can compile against the old and new shape simultaneously. Detection and per-atom resolution both happen inside `LicenseResolutionChain`; `RuleMatcher.MatchExpression` (already built, previously unwired) becomes the rule engine's single entry point for every resolution, one-id or many.

**Tech Stack:** .NET 10, TUnit (`Count().IsEqualTo(n)`, not the obsolete `HasCount().EqualTo(n)`).

**Spec:** `docs/superpowers/specs/2026-09-12-attributary-design.md` (§6, §6a)

## Global Constraints

- Target framework `net10.0`; nullable reference types enabled solution-wide (unchanged, via `Directory.Build.props`).
- TUnit test syntax throughout; pristine build (0 warnings) and pristine test output.
- An expression is eligible for automatic flat-AND resolution only if it contains no `(`, no `" OR "`, no `" WITH "` (case-insensitive), and does contain `" AND "` with 2+ resulting atoms after splitting. Anything else keeps today's `ATT2001` diagnostic behavior exactly as-is (message wording already distinguishes AND-flavored from OR-flavored, unchanged by this plan).
- For a flat-AND component: copyright and notice text resolve once, id-agnostically, via the full existing source chain (unchanged mechanism). License text resolves once per atomic id, using *only* sources where `ILicenseSource.IsLicenseIdSpecific` is `true` (only `SpdxCanonicalSource` today) — never the package-level sources (NuGet/GitHub/license-URL), since they'd hand back the same blob for every atom.
- `RuleMatcher.MatchExpression` becomes `ObligationPlanBuilder`'s only entry point (called with a 1-element list for the ordinary single-license case, producing an identical result to calling `Match` directly — already proven by existing tests).
- No new CLI flags, no new diagnostic codes — `ATT2001`/`ATT3001`/`ATT3002`/`ATT3010` all keep their existing meanings, just now potentially firing per-atom for `ATT3010` where a multi-license component has some atoms resolved and others not.

---

## Task 1: Reshape `LicenseResolution` and wire multi-license resolution in the chain

**Files:**
- Modify: `src/Attributary.Domain/LicenseResolution.cs`
- Modify: `src/Attributary.Resolution/ILicenseSource.cs`
- Modify: `src/Attributary.Resolution/Sources/SpdxCanonicalSource.cs`
- Modify: `src/Attributary.Resolution/Sources/SbomEmbeddedSource.cs`
- Modify: `src/Attributary.Resolution/Sources/NuGetLocalCacheSource.cs`
- Modify: `src/Attributary.Resolution/Sources/GitHubVcsSource.cs`
- Modify: `src/Attributary.Resolution/Sources/SbomLicenseUrlSource.cs`
- Modify: `src/Attributary.Resolution/Sources/SbomEvidenceSource.cs`
- Modify: `src/Attributary.Resolution/Caching/CachingLicenseSource.cs`
- Modify: `src/Attributary.Resolution/LicenseResolutionChain.cs`
- Modify: `tests/Attributary.Resolution.Tests/LicenseResolutionChainTests.cs`
- Modify: `tests/Attributary.Resolution.Tests/Caching/CachingLicenseSourceTests.cs`
- Modify: `tests/Attributary.Resolution.Tests/SpdxCanonicalSourceTests.cs`

**Interfaces:**
- Produces:
  - `record LicenseResolution(SbomComponent Component, IReadOnlyList<string> ResolvedLicenseIds, string? CopyrightText, IReadOnlyDictionary<string, string> LicenseTextsByLicenseId, string? NoticeText, ResolutionProvenance? Provenance = null)` with a static convenience factory `LicenseResolution.ForSingleLicense(SbomComponent component, string? licenseId, string? copyrightText, string? licenseText, string? noticeText, ResolutionProvenance? provenance = null)` — used throughout the test suite wherever a hand-built single-license resolution is needed.
  - `interface ILicenseSource` gains `bool IsLicenseIdSpecific { get; }` — `true` only for `SpdxCanonicalSource`.
- Consumed by every later task in this plan.

- [ ] **Step 1: Reshape `LicenseResolution`**

```csharp
// src/Attributary.Domain/LicenseResolution.cs
namespace Attributary.Domain;

public sealed record LicenseResolution(
    SbomComponent Component,
    IReadOnlyList<string> ResolvedLicenseIds,
    string? CopyrightText,
    IReadOnlyDictionary<string, string> LicenseTextsByLicenseId,
    string? NoticeText,
    ResolutionProvenance? Provenance = null)
{
    public static LicenseResolution ForSingleLicense(
        SbomComponent component,
        string? licenseId,
        string? copyrightText,
        string? licenseText,
        string? noticeText,
        ResolutionProvenance? provenance = null)
    {
        IReadOnlyList<string> ids = licenseId is null ? [] : [licenseId];
        var texts = licenseId is not null && licenseText is not null
            ? new Dictionary<string, string> { [licenseId] = licenseText }
            : new Dictionary<string, string>();
        return new LicenseResolution(component, ids, copyrightText, texts, noticeText, provenance);
    }
}
```

- [ ] **Step 2: Add `IsLicenseIdSpecific` to the source interface and every concrete source**

```csharp
// src/Attributary.Resolution/ILicenseSource.cs
using Attributary.Domain;

namespace Attributary.Resolution;

public interface ILicenseSource
{
    ResolutionSourceStrategy Strategy { get; }
    bool IsLicenseIdSpecific { get; }
    Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct);
}
```

Add `public bool IsLicenseIdSpecific => true;` to `SpdxCanonicalSource` (the only `true`), and `public bool IsLicenseIdSpecific => false;` to `SbomEmbeddedSource`, `NuGetLocalCacheSource`, `GitHubVcsSource`, `SbomLicenseUrlSource`, and `SbomEvidenceSource` — one line added to each existing class body, no other changes to those five files. For `SpdxCanonicalSource.cs`, add it right after the existing `Strategy` property:

```csharp
// src/Attributary.Resolution/Sources/SpdxCanonicalSource.cs — add this member
    public bool IsLicenseIdSpecific => true;
```

And in `CachingLicenseSource.cs` (the decorator — must delegate, not hard-code):

```csharp
// src/Attributary.Resolution/Caching/CachingLicenseSource.cs — add this member
    public bool IsLicenseIdSpecific => inner.IsLicenseIdSpecific;
```

- [ ] **Step 3: Rewrite `LicenseResolutionChain` to detect and resolve flat-AND expressions**

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
            if (TryGetFlatAndAtoms(expression, out var atoms))
                return await ResolveMultiLicenseAsync(component, atoms, ct);

            var message = expression.Contains(" AND ")
                ? $"Component declares a multi-license expression '{expression}' requiring simultaneous compliance with all listed licenses; enrich the SBOM to declare a single component per license, or add explicit support for this combination."
                : $"Component declares an unresolved license expression '{expression}'; enrich the SBOM with a single license id.";
            diagnostics.Report(ResolutionDiagnostics.OrExpressionUnresolved, message, context);
            return new LicenseResolution(component, [], null, new Dictionary<string, string>(), null);
        }

        var licenseId = declared.SpdxId ?? declared.FreeTextName;
        return await ResolveSingleLicenseAsync(component, licenseId, ct);
    }

    // Only a flat top-level AND (no parens, OR, or WITH) is eligible for automatic
    // resolution — anything else keeps asking for SBOM enrichment (spec §6a).
    private static bool TryGetFlatAndAtoms(string expression, out IReadOnlyList<string> atoms)
    {
        atoms = [];
        if (expression.Contains('(')
            || expression.Contains(" OR ", StringComparison.OrdinalIgnoreCase)
            || expression.Contains(" WITH ", StringComparison.OrdinalIgnoreCase)
            || !expression.Contains(" AND ", StringComparison.Ordinal))
        {
            return false;
        }

        var split = expression.Split(" AND ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (split.Length < 2)
            return false;

        atoms = split;
        return true;
    }

    private async Task<LicenseResolution> ResolveSingleLicenseAsync(SbomComponent component, string? licenseId, CancellationToken ct)
    {
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

        IReadOnlyList<string> ids = licenseId is null ? [] : [licenseId];
        var textsById = licenseId is not null && licenseText is not null
            ? new Dictionary<string, string> { [licenseId] = licenseText }
            : new Dictionary<string, string>();
        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);

        return new LicenseResolution(component, ids, copyrightText, textsById, noticeText, provenance);
    }

    private async Task<LicenseResolution> ResolveMultiLicenseAsync(SbomComponent component, IReadOnlyList<string> atomIds, CancellationToken ct)
    {
        // Copyright and notice text are properties of the component as a whole, not
        // of any one license atom -- resolve them once, id-agnostically, via the
        // full chain (spec §6a). Passing licenseId: null is fine: no shipped
        // source's copyright/notice resolution actually branches on it.
        string? copyrightText = null, noticeText = null;
        FieldProvenance? copyrightProvenance = null, noticeProvenance = null;

        foreach (var source in sources)
        {
            var result = await source.TryResolveAsync(component, null, ct);
            if (!result.Resolved) continue;

            if (copyrightText is null && result.CopyrightText is not null) { copyrightText = result.CopyrightText; copyrightProvenance = result.Provenance; }
            if (noticeText is null && result.NoticeText is not null) { noticeText = result.NoticeText; noticeProvenance = result.Provenance; }

            if (copyrightText is not null && noticeText is not null) break;
        }

        // License text: only id-specific sources are safe to ask per atom (see
        // ILicenseSource.IsLicenseIdSpecific) -- a package-level source (NuGet/
        // GitHub/license-URL) would hand back the same blob for every atom, which
        // is wrong, not just imprecise.
        var idSpecificSources = sources.Where(s => s.IsLicenseIdSpecific).ToList();
        var textsById = new Dictionary<string, string>();
        FieldProvenance? licenseProvenance = null;

        foreach (var atomId in atomIds)
        {
            foreach (var source in idSpecificSources)
            {
                var result = await source.TryResolveAsync(component, atomId, ct);
                if (result.Resolved && result.LicenseText is not null)
                {
                    textsById[atomId] = result.LicenseText;
                    licenseProvenance ??= result.Provenance;
                    break;
                }
            }
        }

        var provenance = new ResolutionProvenance(licenseProvenance, copyrightProvenance, noticeProvenance);
        return new LicenseResolution(component, atomIds, copyrightText, textsById, noticeText, provenance);
    }
}
```

- [ ] **Step 4: Update `LicenseResolutionChainTests.cs`** — the fakes need the new interface member, and the existing "AND expression always diagnoses" test's premise is now wrong for a *flat* AND — replace it with tests proving flat-AND auto-resolves while non-flat (OR, parens, WITH) still diagnoses.

```csharp
// tests/Attributary.Resolution.Tests/LicenseResolutionChainTests.cs
using Attributary.Diagnostics;
using Attributary.Domain;

namespace Attributary.Resolution.Tests;

file sealed class FakeSource(ResolutionSourceStrategy strategy, SourceResult result, bool isLicenseIdSpecific = false) : ILicenseSource
{
    public ResolutionSourceStrategy Strategy => strategy;
    public bool IsLicenseIdSpecific => isLicenseIdSpecific;
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
            new FakeSource(ResolutionSourceStrategy.SpdxCanonical, new SourceResult(true, "different text", null, null, provenance), isLicenseIdSpecific: true)
        };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseTextsByLicenseId["MIT"]).IsEqualTo("MIT text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright X");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_NoSourceResolves_LeavesFieldsEmptyWithoutReportingADiagnostic()
    {
        var sources = new ILicenseSource[] { new FakeSource(ResolutionSourceStrategy.SbomEmbedded, SourceResult.Unresolved) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(BuildComponent(LicenseExpression.FromId("MIT")), sink, CancellationToken.None);

        await Assert.That(resolution.LicenseTextsByLicenseId).IsEmpty();
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

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(callCount).IsEqualTo(0);
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("enrich the SBOM with a single license id");
    }

    [Test]
    public async Task ResolveAsync_NonFlatAndExpression_StillReportsMultiLicenseFlavoredMessage()
    {
        // "MIT AND (Apache-2.0 OR BSD-3-Clause)" contains a paren -- not eligible
        // for automatic flat-AND resolution, so it keeps the existing diagnostic.
        var sources = new ILicenseSource[] { new CountingFakeSource(() => { }) };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("MIT AND (Apache-2.0 OR BSD-3-Clause)")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).IsEmpty();
        await Assert.That(sink.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(sink.Diagnostics.Single().Message).Contains("multi-license expression");
    }

    [Test]
    public async Task ResolveAsync_FlatAndExpression_AutoResolvesEachAtomViaIdSpecificSourceOnly()
    {
        var canonicalProvenance = new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false);
        var embeddedProvenance = new FieldProvenance(ResolutionSourceStrategy.SbomEmbedded, null, DateTimeOffset.UtcNow, false);
        var texts = new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" };

        var canonicalSource = new PerAtomFakeSource(texts, canonicalProvenance);
        var packageLevelSource = new FakeSource(
            ResolutionSourceStrategy.SbomEmbedded,
            new SourceResult(true, null, "Copyright Foo Inc.", null, embeddedProvenance));

        var sources = new ILicenseSource[] { packageLevelSource, canonicalSource };
        var chain = new LicenseResolutionChain(sources);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        var resolution = await chain.ResolveAsync(
            BuildComponent(LicenseExpression.FromExpression("MIT AND Apache-2.0")), sink, CancellationToken.None);

        await Assert.That(resolution.ResolvedLicenseIds).Contains("MIT").And.Contains("Apache-2.0");
        await Assert.That(resolution.LicenseTextsByLicenseId["MIT"]).IsEqualTo("MIT text");
        await Assert.That(resolution.LicenseTextsByLicenseId["Apache-2.0"]).IsEqualTo("Apache text");
        await Assert.That(resolution.CopyrightText).IsEqualTo("Copyright Foo Inc.");
        await Assert.That(sink.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task ResolveAsync_FlatAndExpression_PackageLevelSourceNeverAskedForLicenseText()
    {
        // A package-level source (IsLicenseIdSpecific: false) must never be asked
        // for license text in the multi-atom loop -- it would hand back the same
        // blob for every atom, misattributing it.
        var callCountForLicenseText = 0;
        var packageLevelSource = new CallCountingLicenseTextSource(() => callCountForLicenseText++);
        var canonicalSource = new PerAtomFakeSource(
            new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false));

        var chain = new LicenseResolutionChain([packageLevelSource, canonicalSource]);
        var sink = new DiagnosticSink(SeverityOverrides.None);

        await chain.ResolveAsync(BuildComponent(LicenseExpression.FromExpression("MIT AND Apache-2.0")), sink, CancellationToken.None);

        await Assert.That(callCountForLicenseText).IsEqualTo(0);
    }

    private sealed class CountingFakeSource(Action onCalled) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SbomEmbedded;
        public bool IsLicenseIdSpecific => false;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            onCalled();
            return Task.FromResult(SourceResult.Unresolved);
        }
    }

    // Resolves license text per the exact atom id it's asked about, from a fixed
    // lookup table -- stands in for SpdxCanonicalSource's real per-id behavior.
    private sealed class PerAtomFakeSource(IReadOnlyDictionary<string, string> textsById, FieldProvenance provenance) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.SpdxCanonical;
        public bool IsLicenseIdSpecific => true;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            if (licenseId is not null && textsById.TryGetValue(licenseId, out var text))
                return Task.FromResult(new SourceResult(true, text, null, null, provenance));
            return Task.FromResult(SourceResult.Unresolved);
        }
    }

    // A package-level (IsLicenseIdSpecific: false) source that would resolve
    // license text if ever asked -- used to prove the multi-atom loop never asks it.
    private sealed class CallCountingLicenseTextSource(Action onCalledForLicenseText) : ILicenseSource
    {
        public ResolutionSourceStrategy Strategy => ResolutionSourceStrategy.VcsRepository;
        public bool IsLicenseIdSpecific => false;
        public Task<SourceResult> TryResolveAsync(SbomComponent component, string? licenseId, CancellationToken ct)
        {
            onCalledForLicenseText();
            var provenance = new FieldProvenance(Strategy, null, DateTimeOffset.UtcNow, false);
            return Task.FromResult(new SourceResult(true, "would-be-wrong-shared-text", null, null, provenance));
        }
    }
}
```

- [ ] **Step 5: Fix `CachingLicenseSourceTests.cs`'s fake** — its `CountingSource` fake needs the new interface member. Read the existing file, add `public bool IsLicenseIdSpecific => strategy == ResolutionSourceStrategy.SpdxCanonical;` (or however the fake currently determines its strategy) to the `CountingSource` class so it keeps compiling. No behavior in this file needs to change otherwise — the caching decorator's own logic is untouched by this plan.

- [ ] **Step 6: Add a marker assertion to `SpdxCanonicalSourceTests.cs`** proving the new property is actually set correctly on the real class:

```csharp
// tests/Attributary.Resolution.Tests/SpdxCanonicalSourceTests.cs — add this test to the existing class
[Test]
public async Task IsLicenseIdSpecific_IsTrue()
{
    var source = new SpdxCanonicalSource();
    await Assert.That(source.IsLicenseIdSpecific).IsTrue();
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Resolution.Tests`
Expected: PASS — all tests green (existing + 3 new chain tests + 1 new SpdxCanonicalSource test). Do NOT run `dotnet test Attributary.sln` yet — `Attributary.Rules`, `Attributary.Artifacts`, `Attributary.Cli` and their tests will not compile until Tasks 2-5 update them; that is expected at this point in the plan, not a regression.

- [ ] **Step 8: Commit**

```bash
git add src/Attributary.Domain src/Attributary.Resolution tests/Attributary.Resolution.Tests
git commit -m "feat(resolution): reshape LicenseResolution and resolve flat-AND components"
```

---

## Task 2: Wire `RuleMatcher.MatchExpression` into `ObligationPlanBuilder`

**Files:**
- Modify: `src/Attributary.Rules/ObligationPlanBuilder.cs`
- Modify: `src/Attributary.Rules/RuleMatcher.cs`
- Modify: `tests/Attributary.Rules.Tests/ObligationPlanBuilderTests.cs`

**Interfaces:**
- Consumes: `LicenseResolution.ResolvedLicenseIds` (`IReadOnlyList<string>`) and `LicenseResolution.ForSingleLicense(...)` from Task 1.
- Produces: `ObligationPlanBuilder.Build(...)` now calls `matcher.MatchExpression` unconditionally — no other public signature changes in this task.

This project (`Attributary.Rules`) and its test project will not build until this task's changes land, since Task 1 already changed `LicenseResolution`'s shape upstream in `Attributary.Domain`.

- [ ] **Step 1: Update `ObligationPlanBuilder` to call `MatchExpression` unconditionally**

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
        var licenseIds = resolution.ResolvedLicenseIds.Count > 0 ? resolution.ResolvedLicenseIds : ["UNKNOWN"];
        var rule = matcher.MatchExpression(licenseIds, ruleSet);

        var obligations = rule.Require
            .Where(o => o.Condition is null || conditionResults.GetValueOrDefault(o.Condition, false))
            .ToList();

        return new ObligationPlan(resolution, rule.Policy, obligations, rule.Flags);
    }
}
```

- [ ] **Step 2: Remove the stale "not called anywhere yet" comment on `MatchExpression`**

```csharp
// src/Attributary.Rules/RuleMatcher.cs
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

    // The rule engine's universal entry point (ObligationPlanBuilder always calls
    // this, even for a single resolved id — behaviorally identical to Match for a
    // 1-element list). See spec §6a.
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

- [ ] **Step 3: Update `ObligationPlanBuilderTests.cs`** — switch to `LicenseResolution.ForSingleLicense(...)` at both existing call sites, and add a new test proving a multi-license resolution unions obligations and takes the most restrictive policy.

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
                [ObligationFlag.ModificationDisclosure]),
            new LicenseRule("GPL-3.0-only", LicensePolicy.Warn,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [ObligationFlag.SourceOffer, ObligationFlag.CopyleftStrong])
        ]);

    [Test]
    public async Task Build_ConditionResolvesTrue_KeepsConditionalObligation()
    {
        var resolution = LicenseResolution.ForSingleLicense(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", "Notice text");
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = true });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).Contains(ObligationKind.NoticeText);
        await Assert.That(plan.Policy).IsEqualTo(LicensePolicy.Allow);
    }

    [Test]
    public async Task Build_ConditionResolvesFalse_DropsConditionalObligationSilently()
    {
        var resolution = LicenseResolution.ForSingleLicense(BuildComponent("Apache-2.0"), "Apache-2.0", "Copyright Foo Inc.", "Apache text", null);
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = false });

        await Assert.That(plan.Obligations.Select(o => o.Kind)).DoesNotContain(ObligationKind.NoticeText);
    }

    [Test]
    public async Task Build_MultiLicenseResolution_UnionsObligationsAndTakesMostRestrictivePolicy()
    {
        var component = BuildComponent("Apache-2.0"); // DeclaredLicense unused by Build; only ResolvedLicenseIds matters
        var resolution = new LicenseResolution(
            component,
            ["Apache-2.0", "GPL-3.0-only"],
            "Copyright Foo Inc.",
            new Dictionary<string, string> { ["Apache-2.0"] = "Apache text", ["GPL-3.0-only"] = "GPL text" },
            null);
        var builder = new ObligationPlanBuilder(new RuleMatcher());

        var plan = builder.Build(resolution, BuildRuleSet(), new Dictionary<string, bool> { ["upstream-notice-present"] = false });

        await Assert.That(plan.Policy).IsEqualTo(LicensePolicy.Warn);
        await Assert.That(plan.Flags).Contains(ObligationFlag.SourceOffer);
        await Assert.That(plan.Obligations.Select(o => o.Kind).Distinct()).Count().IsEqualTo(2);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Rules.Tests`
Expected: PASS — all tests green, including the 3 existing `RuleMatcherTests` (unaffected by this task) and the updated/new `ObligationPlanBuilderTests`.

- [ ] **Step 5: Commit**

```bash
git add src/Attributary.Rules tests/Attributary.Rules.Tests
git commit -m "feat(rules): make MatchExpression the obligation plan builder's universal entry point"
```

---

## Task 3: Reshape artifact records and builders to carry `LicenseIds`

**Files:**
- Modify: `src/Attributary.Artifacts/AttributionDocument.cs`
- Modify: `src/Attributary.Artifacts/AttributionDocumentBuilder.cs`
- Modify: `src/Attributary.Artifacts/LicenseTextsDocumentBuilder.cs`
- Modify: `src/Attributary.Artifacts/ComplianceReportDocument.cs`
- Modify: `src/Attributary.Artifacts/ComplianceReportDocumentBuilder.cs`
- Modify: `tests/Attributary.Artifacts.Tests/AttributionDocumentBuilderTests.cs`
- Modify: `tests/Attributary.Artifacts.Tests/LicenseTextsDocumentBuilderTests.cs`
- Modify: `tests/Attributary.Artifacts.Tests/ComplianceReportDocumentBuilderTests.cs`
- Modify: `tests/Attributary.Artifacts.Tests/NoticeDocumentBuilderTests.cs`

**Interfaces:**
- Consumes: `LicenseResolution.ResolvedLicenseIds` / `LicenseTextsByLicenseId` from Task 1.
- Produces:
  - `record AttributionRow(string ComponentName, string ComponentVersion, IReadOnlyList<string> LicenseIds, string Copyright)` — `LicenseIds` always has ≥1 entry.
  - `record ComplianceReportEntry(string ComponentName, string ComponentVersion, IReadOnlyList<string> LicenseIds, ResolutionSourceStrategy? LicenseTextSource, IReadOnlyList<ObligationKind> SatisfiedObligations)`.
  - `record ReviewFlagEntry(string ComponentName, string ComponentVersion, IReadOnlyList<string> LicenseIds, IReadOnlyList<ObligationFlag> Flags, LicensePolicy Policy)`.
  - `LicenseTextsDocumentBuilder.Build` dedupes by each individual license id found in any plan's `LicenseTextsByLicenseId`, not by a single per-plan id — this is what makes a two-atom flat-AND component contribute two separate `LicenseTextEntry` rows (one per atom) to the shared licenses collection, same as two different single-license components would.
  - `ComplianceReportEntry.LicenseTextSource` becomes ambiguous for a multi-license component (which atom's provenance would it name?) — keep reporting `resolution.Provenance?.LicenseTextProvenance?.Strategy` as-is; it already reflects only the *first* atom resolved in `ResolveMultiLicenseAsync` (Task 1), which is an acceptable, documented approximation — not a new correctness issue introduced by this task.
- `Attributary.Artifacts` and its tests will not build until this task lands, since it consumes Task 1's `LicenseResolution` shape.

- [ ] **Step 1: Reshape `AttributionDocument.cs`**

```csharp
// src/Attributary.Artifacts/AttributionDocument.cs
namespace Attributary.Artifacts;

public sealed record AttributionRow(string ComponentName, string ComponentVersion, IReadOnlyList<string> LicenseIds, string Copyright);

public sealed record AttributionDocument(
    IReadOnlyList<AttributionRow> Rows,
    IReadOnlyDictionary<string, string> LicenseTextsById,
    bool GroupByLicense,
    bool EmbedLicenseText);
```

- [ ] **Step 2: Update `AttributionDocumentBuilder.cs`**

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
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Resolution.CopyrightText ?? ""))
            .OrderBy(r => r.ComponentName, StringComparer.Ordinal)
            .ToList();

        var licenseTexts = plans
            .SelectMany(p => p.Resolution.LicenseTextsByLicenseId)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);

        return new AttributionDocument(rows, licenseTexts, groupByLicense, embedLicenseText);
    }
}
```

- [ ] **Step 3: Update `LicenseTextsDocumentBuilder.cs`** to dedupe by individual license id across every plan's `LicenseTextsByLicenseId`, and to include a plan only for the atoms it actually has a `LicenseText` obligation and resolved text for

```csharp
// src/Attributary.Artifacts/LicenseTextsDocumentBuilder.cs
using Attributary.Rules;

namespace Attributary.Artifacts;

public static class LicenseTextsDocumentBuilder
{
    public static LicenseTextsDocument Build(IReadOnlyList<ObligationPlan> plans)
    {
        var entries = plans
            .Where(p => p.Obligations.Any(o => o.Kind == ObligationKind.LicenseText))
            .SelectMany(p => p.Resolution.LicenseTextsByLicenseId)
            .GroupBy(kv => kv.Key)
            .Select(g => new LicenseTextEntry(g.Key, g.First().Value))
            .OrderBy(e => e.LicenseId, StringComparer.Ordinal)
            .ToList();

        return new LicenseTextsDocument(entries);
    }
}
```

- [ ] **Step 4: Reshape `ComplianceReportDocument.cs`**

```csharp
// src/Attributary.Artifacts/ComplianceReportDocument.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts;

public sealed record ComplianceReportEntry(
    string ComponentName,
    string ComponentVersion,
    IReadOnlyList<string> LicenseIds,
    ResolutionSourceStrategy? LicenseTextSource,
    IReadOnlyList<ObligationKind> SatisfiedObligations);

public sealed record ReviewFlagEntry(
    string ComponentName,
    string ComponentVersion,
    IReadOnlyList<string> LicenseIds,
    IReadOnlyList<ObligationFlag> Flags,
    LicensePolicy Policy);

public sealed record ComplianceReportDocument(
    IReadOnlyList<ComplianceReportEntry> Entries,
    IReadOnlyList<ReviewFlagEntry> FlaggedForReview);
```

- [ ] **Step 5: Update `ComplianceReportDocumentBuilder.cs`**

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
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Resolution.Provenance?.LicenseTextProvenance?.Strategy,
                p.Obligations.Select(o => o.Kind).ToList()))
            .ToList();

        var flagged = plans
            .Where(p => p.Flags.Count > 0 || p.Policy != LicensePolicy.Allow)
            .Select(p => new ReviewFlagEntry(
                p.Resolution.Component.Name,
                p.Resolution.Component.Version,
                p.Resolution.ResolvedLicenseIds.Count > 0 ? p.Resolution.ResolvedLicenseIds : ["UNKNOWN"],
                p.Flags,
                p.Policy))
            .ToList();

        return new ComplianceReportDocument(entries, flagged);
    }
}
```

- [ ] **Step 6: Update `AttributionDocumentBuilderTests.cs`**

```csharp
// tests/Attributary.Artifacts.Tests/AttributionDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class AttributionDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string copyright, string licenseText) => new(
        LicenseResolution.ForSingleLicense(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromId(licenseId), copyright, [], []),
            licenseId, copyright, licenseText, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    private static ObligationPlan BuildMultiLicensePlan(string name, IReadOnlyList<string> licenseIds, string copyright, IReadOnlyDictionary<string, string> licenseTexts) => new(
        new LicenseResolution(
            new SbomComponent(name, "1.0.0", null, LicenseExpression.FromExpression(string.Join(" AND ", licenseIds)), copyright, [], []),
            licenseIds, copyright, licenseTexts, null),
        LicensePolicy.Allow,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        []);

    [Test]
    public async Task Build_AlwaysIncludesLicenseIdsPerRow_RegardlessOfGrouping()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "Copyright Foo", "MIT text") };

        var flat = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);
        var grouped = AttributionDocumentBuilder.Build(plans, groupByLicense: true, embedLicenseText: false);

        await Assert.That(flat.Rows[0].LicenseIds).Count().IsEqualTo(1);
        await Assert.That(flat.Rows[0].LicenseIds[0]).IsEqualTo("MIT");
        await Assert.That(grouped.Rows[0].LicenseIds[0]).IsEqualTo("MIT");
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

    [Test]
    public async Task Build_MultiLicenseComponent_RowCarriesAllLicenseIdsAndBothTextsAreInDictionary()
    {
        var plans = new[]
        {
            BuildMultiLicensePlan("Foo", ["MIT", "Apache-2.0"], "Copyright Foo",
                new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" })
        };

        var document = AttributionDocumentBuilder.Build(plans, groupByLicense: false, embedLicenseText: false);

        await Assert.That(document.Rows[0].LicenseIds).Contains("MIT").And.Contains("Apache-2.0");
        await Assert.That(document.LicenseTextsById["MIT"]).IsEqualTo("MIT text");
        await Assert.That(document.LicenseTextsById["Apache-2.0"]).IsEqualTo("Apache text");
    }
}
```

- [ ] **Step 7: Update `LicenseTextsDocumentBuilderTests.cs`**

```csharp
// tests/Attributary.Artifacts.Tests/LicenseTextsDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class LicenseTextsDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string licenseId, string licenseText, params ObligationKind[] obligations) => new(
        LicenseResolution.ForSingleLicense(
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

        await Assert.That(document.Licenses).Count().IsEqualTo(1);
        await Assert.That(document.Licenses[0].LicenseId).IsEqualTo("MIT");
    }

    [Test]
    public async Task Build_ComponentWithoutLicenseTextObligation_IsExcluded()
    {
        var plans = new[] { BuildPlan("Foo", "MIT", "MIT text", ObligationKind.Copyright) };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).IsEmpty();
    }

    [Test]
    public async Task Build_MultiLicenseComponent_ContributesOneEntryPerAtom()
    {
        var plans = new[]
        {
            new ObligationPlan(
                new LicenseResolution(
                    new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromExpression("MIT AND Apache-2.0"), "Copyright X", [], []),
                    ["MIT", "Apache-2.0"], "Copyright X",
                    new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" }, null),
                LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [])
        };

        var document = LicenseTextsDocumentBuilder.Build(plans);

        await Assert.That(document.Licenses).Count().IsEqualTo(2);
        await Assert.That(document.Licenses.Select(e => e.LicenseId)).Contains("MIT").And.Contains("Apache-2.0");
    }
}
```

- [ ] **Step 8: Update `ComplianceReportDocumentBuilderTests.cs`**

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
            ["GPL-3.0-only"], "Copyright X",
            new Dictionary<string, string> { ["GPL-3.0-only"] = "GPL text" }, null,
            new ResolutionProvenance(new FieldProvenance(ResolutionSourceStrategy.SpdxCanonical, null, DateTimeOffset.UtcNow, false), null, null)),
        policy,
        [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
        flags);

    [Test]
    public async Task Build_EveryPlan_HasAnEntryWithSatisfiedObligations()
    {
        var plans = new[] { BuildPlan("Foo", LicensePolicy.Allow, []) };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.Entries).Count().IsEqualTo(1);
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

        await Assert.That(report.FlaggedForReview).Count().IsEqualTo(1);
        await Assert.That(report.FlaggedForReview[0].ComponentName).IsEqualTo("Foo");
        await Assert.That(report.FlaggedForReview[0].Flags).Contains(ObligationFlag.SourceOffer);
    }

    [Test]
    public async Task Build_MultiLicenseComponent_EntryCarriesAllLicenseIds()
    {
        var plans = new[]
        {
            new ObligationPlan(
                new LicenseResolution(
                    new SbomComponent("Foo", "1.0.0", null, LicenseExpression.FromExpression("MIT AND Apache-2.0"), "Copyright X", [], []),
                    ["MIT", "Apache-2.0"], "Copyright X",
                    new Dictionary<string, string> { ["MIT"] = "MIT text" }, null),
                LicensePolicy.Allow,
                [new Obligation(ObligationKind.Copyright, null), new Obligation(ObligationKind.LicenseText, null)],
                [])
        };

        var report = ComplianceReportDocumentBuilder.Build(plans);

        await Assert.That(report.Entries[0].LicenseIds).Contains("MIT").And.Contains("Apache-2.0");
    }
}
```

- [ ] **Step 9: Update `NoticeDocumentBuilderTests.cs`** — this file constructs `LicenseResolution` inline and must compile against the new shape, even though `NoticeDocumentBuilder` itself is untouched (notices are resolved id-agnostically and never keyed by license id)

```csharp
// tests/Attributary.Artifacts.Tests/NoticeDocumentBuilderTests.cs
using Attributary.Domain;
using Attributary.Rules;

namespace Attributary.Artifacts.Tests;

public class NoticeDocumentBuilderTests
{
    private static ObligationPlan BuildPlan(string name, string? noticeText, params ObligationKind[] obligations) => new(
        LicenseResolution.ForSingleLicense(
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

        await Assert.That(document.Sections).Count().IsEqualTo(1);
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

- [ ] **Step 10: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Artifacts.Tests`
Expected: PASS — all tests green.

- [ ] **Step 11: Commit**

```bash
git add src/Attributary.Artifacts tests/Attributary.Artifacts.Tests
git commit -m "feat(artifacts): carry LicenseIds through attribution and compliance-report documents"
```

---

## Task 4: Update all 8 output writers to render `LicenseIds`

**Files:**
- Modify: `src/Attributary.Output/Text/TxtAttributionWriter.cs`
- Modify: `src/Attributary.Output/Text/TxtComplianceReportWriter.cs`
- Modify: `src/Attributary.Output/Markdown/MdAttributionWriter.cs`
- Modify: `src/Attributary.Output/Markdown/MdComplianceReportWriter.cs`
- Modify: `src/Attributary.Output/Json/JsonAttributionWriter.cs`
- Modify: `src/Attributary.Output/Html/HtmlAttributionWriter.cs`
- Modify: `src/Attributary.Output/Html/HtmlComplianceReportWriter.cs`
- Modify: `tests/Attributary.Output.Tests/Text/TxtAttributionWriterTests.cs`
- Modify: `tests/Attributary.Output.Tests/Text/TxtComplianceReportWriterTests.cs`
- Modify: `tests/Attributary.Output.Tests/Json/JsonAttributionWriterTests.cs`
- Modify: `tests/Attributary.Output.Tests/Json/JsonComplianceReportWriterTests.cs`

(`JsonComplianceReportWriter.cs` needs no source change — it serializes `ComplianceReportDocument` directly via reflection, so `LicenseIds` already comes out as a real JSON array automatically. Its test still needs the same one-line record-shape update as every other test in this task.)

**Interfaces:**
- Consumes: `AttributionRow.LicenseIds`, `ComplianceReportEntry.LicenseIds`, `ReviewFlagEntry.LicenseIds` (all `IReadOnlyList<string>`) from Task 3.
- Produces: no public signature changes — every writer keeps its existing `Render(...)` signature. Only the internal rendering of the license column/section changes.
- Grouped-mode rendering (Txt/Md/Html attribution writers): a row with N license ids appears once under each of its N license headings — expand before grouping, per spec §6a. Flat-mode rendering: join `LicenseIds` with `" AND "` in the human-readable formats (Txt/Md/Html); the Json attribution writer instead emits a genuine `licenseIds` array.

`Attributary.Output` and its tests will not build until this task lands, since it consumes Task 3's artifact record shapes.

- [ ] **Step 1: Update `TxtAttributionWriter.cs`**

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
            var expanded = document.Rows.SelectMany(r => r.LicenseIds.Select(id => (LicenseId: id, Row: r)));
            foreach (var group in expanded.GroupBy(x => x.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"License: {group.Key}");
                sb.AppendLine(new string('-', 40));
                foreach (var (_, row) in group)
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
                sb.AppendLine($"{row.ComponentName} {row.ComponentVersion} | {string.Join(" AND ", row.LicenseIds)} | {row.Copyright}");

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

- [ ] **Step 2: Update `TxtComplianceReportWriter.cs`**

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
            sb.AppendLine($"{entry.ComponentName} {entry.ComponentVersion} - {string.Join(" AND ", entry.LicenseIds)} (source: {(entry.LicenseTextSource?.ToString() ?? "unresolved")})");

        sb.AppendLine();
        sb.AppendLine("Flagged for review");
        sb.AppendLine(new string('-', 40));
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"{flag.ComponentName} {flag.ComponentVersion} - policy: {flag.Policy}, flags: {string.Join(", ", flag.Flags)}");

        return sb.ToString();
    }
}
```

- [ ] **Step 3: Update `MdAttributionWriter.cs`**

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
            var expanded = document.Rows.SelectMany(r => r.LicenseIds.Select(id => (LicenseId: id, Row: r)));
            foreach (var group in expanded.GroupBy(x => x.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                foreach (var (_, row) in group)
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
            {
                var licenseCell = string.Join(" AND ", row.LicenseIds.Select(id => $"[{id}](LICENSES/{id}.txt)"));
                sb.AppendLine($"| {row.ComponentName} | {row.ComponentVersion} | {licenseCell} | {row.Copyright} |");
            }
        }

        return sb.ToString();
    }
}
```

- [ ] **Step 4: Update `MdComplianceReportWriter.cs`**

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
            sb.AppendLine($"| {entry.ComponentName} | {entry.ComponentVersion} | {string.Join(" AND ", entry.LicenseIds)} | {(entry.LicenseTextSource?.ToString() ?? "unresolved")} |");

        sb.AppendLine();
        sb.AppendLine("## Flagged for review");
        sb.AppendLine();
        sb.AppendLine("| Component | Version | License | Policy | Flags |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"| {flag.ComponentName} | {flag.ComponentVersion} | {string.Join(" AND ", flag.LicenseIds)} | {flag.Policy} | {string.Join(", ", flag.Flags)} |");

        return sb.ToString();
    }
}
```

- [ ] **Step 5: Update `JsonAttributionWriter.cs`** to emit a genuine `licenseIds` array

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
        Converters = { new JsonStringEnumConverter() }
    };

    public OutputFormat Format => OutputFormat.Json;

    public string Render(AttributionDocument document)
    {
        var payload = new
        {
            components = document.Rows.Select(r => new { r.ComponentName, r.ComponentVersion, r.LicenseIds, r.Copyright }),
            licenses = document.LicenseTextsById.ToDictionary(
                kv => kv.Key,
                kv => document.EmbedLicenseText ? kv.Value : (string?)null)
        };
        return JsonSerializer.Serialize(payload, Options);
    }
}
```

- [ ] **Step 6: Update `HtmlAttributionWriter.cs`**

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
            var expanded = document.Rows.SelectMany(r => r.LicenseIds.Select(id => (LicenseId: id, Row: r)));
            foreach (var group in expanded.GroupBy(x => x.LicenseId).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                sb.AppendLine($"<h2>{Encode(group.Key)}</h2>");
                sb.AppendLine("<ul>");
                foreach (var (_, row) in group)
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
            {
                var licenseCell = string.Join(" AND ", row.LicenseIds.Select(id =>
                    $"<a href=\"LICENSES/{Encode(id)}.txt\">{Encode(id)}</a>"));
                sb.AppendLine($"<tr><td>{Encode(row.ComponentName)}</td><td>{Encode(row.ComponentVersion)}</td>"
                    + $"<td>{licenseCell}</td><td>{Encode(row.Copyright)}</td></tr>");
            }
            sb.AppendLine("</table>");
        }

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
```

- [ ] **Step 7: Update `HtmlComplianceReportWriter.cs`**

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
                + $"<td>{Encode(string.Join(" AND ", entry.LicenseIds))}</td><td>{Encode(entry.LicenseTextSource?.ToString() ?? "unresolved")}</td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h2>Flagged for review</h2>");
        sb.AppendLine("<table><tr><th>Component</th><th>Version</th><th>License</th><th>Policy</th><th>Flags</th></tr>");
        foreach (var flag in document.FlaggedForReview)
            sb.AppendLine($"<tr><td>{Encode(flag.ComponentName)}</td><td>{Encode(flag.ComponentVersion)}</td>"
                + $"<td>{Encode(string.Join(" AND ", flag.LicenseIds))}</td><td>{Encode(flag.Policy.ToString())}</td><td>{Encode(string.Join(", ", flag.Flags))}</td></tr>");
        sb.AppendLine("</table>");

        return sb.ToString();
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
```

- [ ] **Step 8: Update `TxtAttributionWriterTests.cs`**

```csharp
// tests/Attributary.Output.Tests/Text/TxtAttributionWriterTests.cs
using Attributary.Artifacts;
using Attributary.Output.Text;

namespace Attributary.Output.Tests.Text;

public class TxtAttributionWriterTests
{
    [Test]
    public async Task Render_GroupedWithEmbed_ShowsLicenseHeadingAndFullText()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
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
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT | Copyright Foo");
        await Assert.That(result).DoesNotContain("MIT full text");
    }

    [Test]
    public async Task Render_MultiLicenseRow_FlatModeJoinsIdsWithAnd()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: false, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 | MIT AND Apache-2.0 | Copyright Foo");
    }

    [Test]
    public async Task Render_MultiLicenseRow_GroupedModeListsComponentUnderEachLicenseHeading()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new TxtAttributionWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("License: MIT");
        await Assert.That(result).Contains("License: Apache-2.0");
        var fooOccurrences = result.Split("Foo 1.0.0").Length - 1;
        await Assert.That(fooOccurrences).IsEqualTo(2);
    }
}
```

- [ ] **Step 9: Update `TxtComplianceReportWriterTests.cs`**

```csharp
// tests/Attributary.Output.Tests/Text/TxtComplianceReportWriterTests.cs
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Text;
using Attributary.Rules;

namespace Attributary.Output.Tests.Text;

public class TxtComplianceReportWriterTests
{
    [Test]
    public async Task Render_IncludesEntriesAndFlaggedForReviewSections()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["GPL-3.0-only"], ResolutionSourceStrategy.SpdxCanonical, [ObligationKind.Copyright])],
            FlaggedForReview: [new ReviewFlagEntry("Foo", "1.0.0", ["GPL-3.0-only"], [ObligationFlag.SourceOffer], LicensePolicy.Warn)]);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - GPL-3.0-only (source: SpdxCanonical)");
        await Assert.That(result).Contains("policy: Warn, flags: SourceOffer");
    }

    [Test]
    public async Task Render_MultiLicenseEntry_JoinsIdsWithAnd()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["MIT", "Apache-2.0"], ResolutionSourceStrategy.SpdxCanonical, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new TxtComplianceReportWriter();

        var result = writer.Render(document);

        await Assert.That(result).Contains("Foo 1.0.0 - MIT AND Apache-2.0 (source: SpdxCanonical)");
    }
}
```

- [ ] **Step 10: Update `JsonAttributionWriterTests.cs`**

```csharp
// tests/Attributary.Output.Tests/Json/JsonAttributionWriterTests.cs
using System.Text.Json;
using Attributary.Artifacts;
using Attributary.Output.Json;

namespace Attributary.Output.Tests.Json;

public class JsonAttributionWriterTests
{
    [Test]
    public async Task Render_EmbedTrue_IncludesFullLicenseTextInPayload()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
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
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT full text" },
            GroupByLicense: true, EmbedLicenseText: false);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("licenses").GetProperty("MIT").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Render_MultiLicenseRow_EmitsLicenseIdsAsJsonArray()
    {
        var document = new AttributionDocument(
            Rows: [new AttributionRow("Foo", "1.0.0", ["MIT", "Apache-2.0"], "Copyright Foo")],
            LicenseTextsById: new Dictionary<string, string> { ["MIT"] = "MIT text", ["Apache-2.0"] = "Apache text" },
            GroupByLicense: false, EmbedLicenseText: true);
        var writer = new JsonAttributionWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        var ids = doc.RootElement.GetProperty("components")[0].GetProperty("licenseIds")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        await Assert.That(ids).Contains("MIT").And.Contains("Apache-2.0");
    }
}
```

- [ ] **Step 11: Update `JsonComplianceReportWriterTests.cs`**

```csharp
// tests/Attributary.Output.Tests/Json/JsonComplianceReportWriterTests.cs
using System.Text.Json;
using Attributary.Artifacts;
using Attributary.Domain;
using Attributary.Output.Json;
using Attributary.Rules;

namespace Attributary.Output.Tests.Json;

public class JsonComplianceReportWriterTests
{
    [Test]
    public async Task Render_SerializesEntriesWithEnumsAsStrings()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["MIT"], ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new JsonComplianceReportWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        await Assert.That(doc.RootElement.GetProperty("entries")[0].GetProperty("licenseTextSource").GetString()).IsEqualTo("SbomEmbedded");
    }

    [Test]
    public async Task Render_MultiLicenseEntry_LicenseIdsIsJsonArray()
    {
        var document = new ComplianceReportDocument(
            Entries: [new ComplianceReportEntry("Foo", "1.0.0", ["MIT", "Apache-2.0"], ResolutionSourceStrategy.SbomEmbedded, [ObligationKind.Copyright])],
            FlaggedForReview: []);
        var writer = new JsonComplianceReportWriter();

        var result = writer.Render(document);
        using var doc = JsonDocument.Parse(result);

        var ids = doc.RootElement.GetProperty("entries")[0].GetProperty("licenseIds")
            .EnumerateArray().Select(e => e.GetString()).ToList();
        await Assert.That(ids).Contains("MIT").And.Contains("Apache-2.0");
    }
}
```

- [ ] **Step 12: Run tests to verify they pass**

Run: `dotnet test tests/Attributary.Output.Tests`
Expected: PASS — all tests green.

- [ ] **Step 13: Commit**

```bash
git add src/Attributary.Output tests/Attributary.Output.Tests
git commit -m "feat(output): render multi-license rows across all writers"
```

---

## Task 5: Wire the CLI orchestrator to the new shape, verify the full solution end-to-end

**Files:**
- Modify: `src/Attributary.Cli/Pipeline/GenerateOrchestrator.cs`
- Modify: `tests/Attributary.Cli.Tests/Pipeline/GenerateOrchestratorTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: nothing new downstream — this is the final task. It is the only task in this plan that gates on a full-solution build and test run.

`GenerateRunner.cs` needs **no changes** — it references `output.LicenseTexts.Licenses`, `output.Attribution`, `output.Report` only as opaque values it hands to writers/file paths; none of its own code names `ResolvedLicenseId` or `LicenseId` directly. `GenerateRunnerTests.cs` likewise needs no changes — its assertions check exit codes and file existence, never field shapes.

- [ ] **Step 1: Update `GenerateOrchestrator.cs`** to use `ResolvedLicenseIds` and to check `LicenseText` obligation resolution per atom

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

            if (resolution.ResolvedLicenseIds.Count > 0)
            {
                var context = $"{component.Name} {component.Version}";
                ReportPolicyDiagnostic(plan, context);
                ReportUnresolvedObligations(plan, context);
            }

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
        var licenseLabel = string.Join(" AND ", plan.Resolution.ResolvedLicenseIds);
        if (plan.Policy == LicensePolicy.Deny)
            diagnostics.Report(RuleEngineDiagnostics.PolicyDenied, $"License '{licenseLabel}' is denied by policy.", context);
        else if (plan.Policy == LicensePolicy.Warn)
            diagnostics.Report(RuleEngineDiagnostics.PolicyWarn, $"License '{licenseLabel}' requires manual review.", context);
    }

    private void ReportUnresolvedObligations(ObligationPlan plan, string context)
    {
        foreach (var obligation in plan.Obligations)
        {
            if (obligation.Kind == ObligationKind.LicenseText)
            {
                // Checked per atom: a multi-license component can have some
                // license ids resolved and others not (spec §6a).
                foreach (var licenseId in plan.Resolution.ResolvedLicenseIds)
                {
                    if (!plan.Resolution.LicenseTextsByLicenseId.ContainsKey(licenseId))
                        diagnostics.Report(RuleEngineDiagnostics.RequiredObligationUnresolved,
                            $"Required obligation 'LicenseText' could not be resolved for license '{licenseId}'.", context);
                }
                continue;
            }

            var resolved = obligation.Kind switch
            {
                ObligationKind.Copyright => plan.Resolution.CopyrightText is not null,
                ObligationKind.NoticeText => plan.Resolution.NoticeText is not null,
                _ => true
            };

            if (!resolved)
            {
                var licenseLabel = string.Join(" AND ", plan.Resolution.ResolvedLicenseIds);
                diagnostics.Report(RuleEngineDiagnostics.RequiredObligationUnresolved,
                    $"Required obligation '{obligation.Kind}' could not be resolved for license '{licenseLabel}'.", context);
            }
        }
    }
}
```

- [ ] **Step 2: Add end-to-end flat-AND tests to `GenerateOrchestratorTests.cs`**

The bundled offline SPDX subset (`spdx-subset.json`) currently contains exactly `MIT` and `BSD-3-Clause` (verified by inspection at plan-writing time — if a later change adds more entries, these tests remain valid since they only rely on these two ids being present, not on the set being exactly these two). Add these tests to the existing class, after `RunAsync_UnresolvableLicenseExpression_ReportsExactlyOneDiagnosticButStillIncludesComponentInReport`:

```csharp
    [Test]
    public async Task RunAsync_FlatAndExpression_BothAtomsResolvable_AutoResolvesWithNoDiagnostics()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Quux", "version": "5.0.0",
                  "copyright": "Copyright (c) Quux Inc.",
                  "licenses": [ { "expression": "MIT AND BSD-3-Clause" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics).IsEmpty();
        await Assert.That(output.Attribution.Rows[0].LicenseIds).Contains("MIT").And.Contains("BSD-3-Clause");
        await Assert.That(output.LicenseTexts.Licenses).Count().IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_FlatAndExpression_OneAtomUnresolvable_ReportsRequiredObligationUnresolvedForThatAtomOnly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Corge", "version": "6.0.0",
                  "copyright": "Copyright (c) Corge Inc.",
                  "licenses": [ { "expression": "MIT AND ISC" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics.Select(d => d.Descriptor.Code)).Contains("ATT3010");
        await Assert.That(diagnostics.Diagnostics.Single(d => d.Descriptor.Code == "ATT3010").Message).Contains("ISC");
        await Assert.That(output.LicenseTexts.Licenses.Select(e => e.LicenseId)).Contains("MIT");
        await Assert.That(output.LicenseTexts.Licenses.Select(e => e.LicenseId)).DoesNotContain("ISC");
    }

    [Test]
    public async Task RunAsync_NonFlatAndExpression_StillReportsAtt2001NotAnAutoResolve()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attributary-{Guid.NewGuid()}.cdx.json");
        File.WriteAllText(path, """
            {
              "bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
              "components": [
                { "type": "library", "name": "Grault", "version": "7.0.0",
                  "copyright": "Copyright (c) Grault Inc.",
                  "licenses": [ { "expression": "MIT AND (Apache-2.0 OR BSD-3-Clause)" } ] }
              ]
            }
            """);
        var chain = new LicenseResolutionChain([new SbomEmbeddedSource(), new SpdxCanonicalSource()]);
        var diagnostics = new DiagnosticSink(SeverityOverrides.None);
        var orchestrator = new GenerateOrchestrator(new CycloneDxIngestor(), chain, new ObligationPlanBuilder(new RuleMatcher()), diagnostics);
        var ruleSet = new DefaultRuleSetProvider(new YamlRuleSetLoader()).Load();

        var output = await orchestrator.RunAsync(path, ruleSet, groupByLicense: false, embedLicenseText: true, failFast: false, CancellationToken.None);

        await Assert.That(diagnostics.Diagnostics).Count().IsEqualTo(1);
        await Assert.That(diagnostics.Diagnostics.Single().Descriptor.Code).IsEqualTo("ATT2001");
        await Assert.That(output.Attribution.Rows[0].LicenseIds).Count().IsEqualTo(1);
        await Assert.That(output.Attribution.Rows[0].LicenseIds[0]).IsEqualTo("UNKNOWN");
    }
```

- [ ] **Step 3: Run the affected project's tests to verify they pass**

Run: `dotnet test tests/Attributary.Cli.Tests`
Expected: PASS — all tests green, including the 3 new flat-AND end-to-end tests and the pre-existing `GenerateRunnerTests` (unmodified, still valid).

- [ ] **Step 4: Run the FULL solution build and test suite — this is the plan's only whole-solution gate**

Run: `dotnet build Attributary.sln` then `dotnet test Attributary.sln`
Expected: PASS — 0 build warnings, every test project green. This is the first point in this plan where the whole solution is expected to compile and pass together, since Task 1's `LicenseResolution` reshape cascaded through every downstream project and each earlier task's gate was intentionally scoped to only its own directly-affected test project.

- [ ] **Step 5: Update the spec's stale in-code comment cross-reference, if any remain**

Run: `git grep -n "deferred v2 item\|Not called anywhere yet" -- src` and confirm no results (the one known instance was removed in Task 2, Step 2). If any other stale reference to `MatchExpression` being unwired is found anywhere in `src/` or `docs/superpowers/specs/2026-09-12-attributary-design.md`, update it to reflect that it is now the universal entry point.

- [ ] **Step 6: Commit**

```bash
git add src/Attributary.Cli tests/Attributary.Cli.Tests
git commit -m "feat(cli): wire orchestrator to multi-license resolution; full-solution AND-composition support complete"
```

---
