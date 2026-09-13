# Attributary — Design Spec

Date: 2026-09-12
Status: Approved for planning

## 1. Purpose

Attributary is a .NET 10 CLI tool that reads a CycloneDX SBOM describing a
distributed software product and produces the artifacts needed for open
source license compliance: per-license license text, an aggregated NOTICE
file (where triggered), a self-contained attribution document, and a
human-readable compliance report. It is driven by a rule engine ("the
obligations DSL") that encodes, per license, what must be produced and what
must merely be flagged for human review.

Primary distribution scenario: a compiled/binary .NET application shipped to
customers. SBOM components are expected to be primarily NuGet packages, plus
native C/C++ libraries that may have no package-manager or VCS metadata at
all (e.g. copied from a file share).

## 2. Non-goals (v1)

- **No source code redistribution.** Copyleft licenses that require offering
  source (GPL, LGPL, MPL, CDDL, EPL) are handled by *flagging* the
  obligation for human review, not by fetching/bundling source or generating
  a written-offer document. (`SOURCE-OFFER.txt` generation is a plausible
  v2 feature, not built now.)
- **No manual-overrides subsystem.** When SBOM data is insufficient to
  resolve a component's license/copyright, the tool emits a diagnostic
  rather than prompting interactively or reading a per-component override
  file. The expected remediation is enriching the SBOM itself (or adding a
  rule engine entry, for policy-level decisions).
- **No `.editorconfig` support.** Diagnostic severity configuration lives in
  Attributary's own config file plus CLI flags.
- **No automatic SPDX OR-expression resolution.** A component whose license
  is an unresolved expression (e.g. `"(MIT OR Apache-2.0)"`) always produces
  a diagnostic asking for a single concrete `license.id` in the SBOM. The
  rule engine never guesses a branch.
- **No richer CC-BY-style attribution schema** (author/title/source-URI/
  license-URI) in v1. The generic per-component attribution row is
  sufficient for code dependencies; revisit if bundled fonts/docs/data
  become a real use case.

## 3. Architecture: bounded contexts

Seven independent modules, each testable without the others, communicating
through the domain model types defined in §4:

1. **Diagnostics** — diagnostic code registry, severity model (default +
   overrides), severity resolution, MSBuild-style formatting/output. No
   dependency on SBOM or license concepts; usable standalone.
2. **SBOM Ingestion** — wraps the official CycloneDX NuGet package; parses
   CycloneDX JSON/XML across supported spec versions (1.2–1.6) and produces
   `SbomComponent` values. Isolates the rest of the app from the CycloneDX
   object model and from format/version differences.
3. **License Resolution** — resolves a `SbomComponent`'s license
   expression, license text, copyright, and (when applicable) notice text,
   via an ordered chain of source strategies (§6). Produces
   `LicenseResolution`.
4. **Rule Engine** — pure, no I/O. Given a `LicenseResolution`'s resolved
   SPDX ID (or free-text license name), looks up a `LicenseRule` (bundled
   defaults + user config overrides) and produces an `ObligationPlan`:
   required data obligations, informational flags, and a policy verdict
   (allow/warn/deny).
5. **Artifact Generation** — turns the full set of `ObligationPlan`s into
   format-agnostic document models: license texts (deduped), the NOTICE
   document, the attribution document, the compliance report.
6. **Output Writers** — per-format renderers (txt/md/json/html) for each
   document model. Know nothing about CycloneDX, the rule engine, or
   sourcing strategies — only how to render a document model.
7. **CLI / composition root** — Spectre.Console.Cli commands wiring the
   above together; Spectre.Console for tables, progress, and diagnostic
   summaries.

Data flows strictly left to right: Ingestion → Resolution → Rule Engine →
Artifact Generation → Output Writers. Each stage's output type wraps
(references) its input rather than mutating a shared object — see §4.

## 4. Domain model

```csharp
public sealed record SbomComponent(
    string Name,
    string Version,
    string? Purl,
    LicenseExpression DeclaredLicense,     // parsed SPDX id / name / AND-OR expression
    string? RawCopyright,
    IReadOnlyList<ExternalReference> ExternalReferences,  // vcs, license, website, ...
    IReadOnlyList<LicenseEvidence> Evidence);              // CycloneDX 1.5+ evidence.licenses

public sealed record LicenseResolution(
    SbomComponent Component,
    string? ResolvedLicenseId,     // SPDX id, or free-text name if non-SPDX
    string? CopyrightText,
    string? LicenseText,
    string? NoticeText,
    ResolutionProvenance Provenance);  // which source strategy resolved each field, cache hit/miss, fetch time, source URL

public sealed record ObligationPlan(
    LicenseResolution Resolution,
    LicensePolicy Policy,          // Allow | Warn | Deny
    IReadOnlyList<Obligation> Obligations,
    IReadOnlyList<ObligationFlag> Flags);
```

`SbomComponent` is the SBOM Ingestion domain's output type only — later
stages never mutate it, they wrap it. This keeps every stage's output
independently constructible in tests (e.g. the Rule Engine can be tested
against a hand-built `LicenseResolution` with no real SBOM or network
involved).

## 5. The obligations DSL (rule engine)

Every obligation is one of two kinds:

- **Data obligations** (`copyright`, `license-text`, `notice-text`) —
  something that must resolve to a value for the component. Each feeds a
  specific artifact:

  | Obligation | Feeds |
  |---|---|
  | `license-text` | `LICENSES/<SPDX-ID>.txt` (deduped globally by license id) |
  | `copyright` | the component's row in the attribution document |
  | `notice-text` | the aggregated `NOTICE.txt` |

  An unresolved required data obligation is a diagnostic (default severity:
  Error — see §8).

- **Flags** (informational only, never block generation): `source-offer`,
  `modification-disclosure`, `non-endorsement`, `trademark-non-grant`,
  `patent-grant`, `advertising-clause`, `non-osi-approved`,
  `copyleft-weak`, `copyleft-strong`. Surfaced in the compliance report's
  "flagged for review" section and as low-severity diagnostics.

Any required obligation entry may carry an optional `when` condition,
evaluated by the License Resolution stage (not authored per-component). If
the condition resolves false, the obligation silently drops from the plan
— no diagnostic. This is how Apache-2.0's actual NOTICE trigger is modeled
correctly: propagation is required *only if the upstream component itself
ships a NOTICE*, which is the only mainstream license with a legally
defined NOTICE-file mechanism (research: Apache License 2.0 §4(d)). No
other common license needs this obligation, but it is modeled generically
(any rule may attach `notice-text: { when: ... }`) rather than
Apache-special-cased, so it extends to other licenses without code changes.

### Schema

```yaml
defaults:
  unknownLicense:
    policy: deny              # allow | warn | deny — safe default: an SBOM
                               # license with no matching rule fails the run
                               # until the SBOM is fixed, a rule is added, or
                               # the license is explicitly allowed.
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

  - id: BSD-4-Clause
    policy: warn              # rescinded by UC Berkeley (1999), GPL-incompatible
    require: [copyright, license-text]
    flags: [advertising-clause]

  - id: GPL-3.0-*             # glob matches GPL-3.0-only, GPL-3.0-or-later
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-strong]

  - id: JSON
    policy: deny
    require: [copyright, license-text]
    flags: [non-osi-approved]
```

`id` matching: exact SPDX identifier, a simple trailing-`*` glob for
version-family variants (e.g. `GPL-3.0-*`), or a free-text license name for
non-SPDX/proprietary licenses (matched as an opaque string). Rule lookup
order: exact id → glob match → `defaults.unknownLicense`.

**Rule composition for combined expressions.** A component with an AND
expression (e.g. the legacy dual OpenSSL+SSLeay license) merges the rules of
all named licenses: obligations and flags union, policy takes the most
restrictive value (`deny` > `warn` > `allow`). A component with an
unresolved OR expression never reaches rule matching — it is stopped at
License Resolution with a diagnostic (§2, §6).

## 6. License/copyright/notice sourcing pipeline

Ordered fallback chain, each strategy either resolves a field or hands off
to the next:

1. **SBOM-embedded** — `component.licenses[].license.text` (if a scanner
   embedded full text) and `component.evidence.licenses[]` (CycloneDX
   1.5+, lower-confidence corroboration), plus `component.copyright`.
   Free, offline, authoritative for what's already in the SBOM.
2. **Local package cache** — ecosystem-specific, no configuration
   required. For NuGet: read the `.nuspec` and any embedded license file
   from the global-packages folder (or a scratch restore) for the exact
   package/version — the fastest and most exact source when available.
3. **VCS repository** — via `externalReferences[type=vcs]`: GitHub REST
   `GET /repos/{owner}/{repo}/license` when the repo is on GitHub; a
   raw-file guess (`LICENSE`, `LICENSE.md`, `NOTICE`, ...) for other Git
   hosts. A `externalReferences[type=license]` entry, when present, is
   consulted first as a direct URL — cheaper than VCS guessing.
4. **SPDX canonical text** — a vendored offline snapshot of
   `spdx/license-list-data`'s per-ID license text, keyed by SPDX id. This
   is the terminal, always-available fallback for `license-text`; it can
   never satisfy a component-specific `copyright` obligation (the text is
   generic, with no copyright holder filled in).
5. **Exhausted** — the obligation is unresolved; diagnostic emitted
   (default severity per §8), pointing at SBOM enrichment as the fix.

Native/file-share components with no package manager or VCS metadata skip
straight from step 1 to step 4/5 — for these, `license-text` may still
resolve generically via SPDX canonical text, but `copyright` will very
often fail and surface a diagnostic, which is expected and intentional
(push back to enriching the SBOM with a `copyright` field for that
component).

### Caching

- **Key**: SPDX-canonical text keys by `(strategy, SPDX-id)` — shared
  across every component using that license. VCS/registry-sourced text and
  copyright key by `(strategy, PURL, version)` — specific to that exact
  package instance.
- **Location**: an OS-standard cache directory
  (`%LOCALAPPDATA%\Attributary\cache` on Windows, `~/.cache/attributary`
  elsewhere), overridable via `--cache-dir` (for CI runners that persist a
  cache directory between jobs).
- **Integrity**: each entry has a sidecar recording SHA-256 of the cached
  content, the source strategy, source URL, and fetch timestamp. On read,
  the hash is recomputed; a mismatch invalidates the entry (re-resolved,
  with a diagnostic). This detects corruption, not tampering by an
  attacker with write access to the cache directory — the same trust model
  NuGet's and npm's local caches operate under. The provenance fields also
  feed the compliance report, so a reviewer can see where cached text
  originally came from.
- **CLI**: `--no-cache` on `generate` bypasses the cache entirely for that
  run (no read, no write). `attributary cache clear|list|path` is a
  separate verb group for cache maintenance.

## 7. Output structure & naming

```
<out>/
  LICENSES/
    MIT.txt
    Apache-2.0.txt
    BSD-3-Clause.txt
  NOTICE.txt                    # written only if >=1 component triggered a notice-text obligation
  THIRD-PARTY-NOTICES.<ext>     # attribution document, one per --format requested
  COMPLIANCE-REPORT.<ext>       # human/audit-facing receipt, one per --format requested
```

- **`LICENSES/<SPDX-ID>.txt`** — REUSE-spec convention verbatim: exact
  SPDX identifier as filename, always plain text (canonical legal text,
  not a rendered document). Deduplicated globally — one file per distinct
  license actually used across all components.
- **`NOTICE.txt`** — single aggregated file (not a folder), one delimited
  section per contributing component's notice text, matching Apache's own
  guidance and real-world practice (Chromium, Android). Always plain text.
- **`THIRD-PARTY-NOTICES.<ext>`** — the attribution document. Every
  component row always shows its resolved license id/name (for linking,
  regardless of grouping mode) and copyright.
  - Grouped by license by default (`--group-by license|component`; default
    `license`) — heading per license, components listed under it, matching
    ORT/cargo-about prior art.
  - Full license text embedded inline per group by default
    (`--embed-license-text` / `--no-embed-license-text`; default: embed)
    — matches the established Microsoft/.NET `THIRD-PARTY-NOTICES.TXT`
    convention, making the file self-contained even without `LICENSES/`
    attached. Text/HTML/Markdown honor both flags; JSON output ignores
    `--group-by` (always fully structured: component list + license
    dictionary) but still respects `--embed-license-text` for whether full
    text is duplicated in the JSON payload.
- **`COMPLIANCE-REPORT.<ext>`** — every component, its resolved license and
  source-strategy provenance, which obligations were satisfied by which
  artifact, and a distinct "flagged for review" section for components
  carrying informational flags or a `warn`/`deny` policy verdict.
- **`--format`** accepts a comma list (e.g. `--format md,html,json`);
  `NOTICE.txt` and `LICENSES/*.txt` are always plain text regardless of
  `--format`, since they are literal legal text, not rendered documents.

## 8. Diagnostics

Diagnostic codes are ranged by pipeline stage so the number alone indicates
the subsystem that raised it:

| Range | Stage | Example |
|---|---|---|
| ATT0xxx | Config/rules-file loading | `ATT0002` error: unknown obligation name in rules file |
| ATT1xxx | SBOM ingestion | `ATT1001` error: unsupported/unparseable CycloneDX version |
| ATT2xxx | License resolution | `ATT2001` error: unresolved SPDX OR-expression; `ATT2500` warning: cache entry failed integrity check, re-resolving |
| ATT3xxx | Rule engine / policy | `ATT3001` error: license policy `deny`; `ATT3002` warning: license policy `warn`; `ATT3010` error: required data obligation unresolved |
| ATT4xxx | Artifact generation / output | `ATT4001` error: output path not writable |

Every diagnostic has a default severity (Error/Warning/Info). Effective
severity is resolved as:

```
explicit --severity CODE=Level  >  --nowarn:CODE  >  --warnaserror-:CODE (exemption)  >  --warnaserror[:CODE]  >  built-in default
```

Configurable both via CLI flags and via a `diagnostics:` section in the app
config file (persisted, checked in). No `.editorconfig` support.

`generate` (and `generate --dry-run`, see §9) collect every diagnostic
across all components before failing — MSBuild-style "keep going, report
everything, then fail" — unless `--fail-fast` is passed, which
short-circuits at the first *effective* Error (post severity-resolution, so
a warning promoted to error by `--warnaserror` also triggers it). Output
ends in a Spectre `Table` summary (`N error(s), M warning(s)`) plus
per-diagnostic lines formatted as `ATT#### severity: message [component]`.

## 9. CLI command surface

- **`attributary generate --sbom <path> --out <dir> [--format md,html,json] [--group-by license|component] [--embed-license-text|--no-embed-license-text] [--dry-run] [--fail-fast] [--no-cache] [--cache-dir <path>] [--warnaserror[:CODE,...]] [--warnaserror-:CODE,...] [--nowarn:CODE,...] [--severity CODE=Level] [--config <path>]`**
  Runs the full pipeline and writes output files. `--dry-run` runs the
  identical pipeline through Rule Engine, then skips Output Writers,
  printing a preview table (paths that would be written, formats,
  obligation counts) instead of touching disk — this is the tool's only
  "check without writing" mode; there is no separate `validate` verb.
- **`attributary license list --sbom <path>`** — table of every license
  found in the SBOM against its resolved rule (default vs. override) and
  policy verdict; audits coverage without a full generate run.
- **`attributary license show <SPDX-ID>`** — inspects what a rule would
  require (obligations, flags, policy) using just the rules file and
  bundled SPDX dataset, independent of any SBOM. Useful while authoring
  custom rules.
- **`attributary cache clear|list|path`** — cache maintenance, separate
  from generation.
- **`attributary init`** — scaffolds a starter config file (rules seeded
  with the bundled defaults, plus `diagnostics`, `output`, and `cache`
  sections) in the current directory.

All rule engine, diagnostics, output, and cache settings live in one config
file (default `attributary.config.yaml`, overridable via `--config`), with
top-level sections `rules`, `diagnostics`, `output`, `cache`. CLI flags
override config file values for that run.

## 10. Testing approach

Each bounded context is tested in isolation:

- **Diagnostics**: pure unit tests of severity resolution (defaults +
  override precedence), no other domain involved.
- **SBOM Ingestion**: parse fixture SBOMs (JSON/XML, several CycloneDX
  spec versions) into `SbomComponent`, verifying normalization.
- **License Resolution**: each source strategy tested independently against
  fakes/fixtures; the fallback chain tested by composing fakes that
  deliberately miss to exercise hand-off and the terminal diagnostic.
- **Rule Engine**: pure function tests — hand-built `LicenseResolution` →
  expected `ObligationPlan`, including glob matching, AND-composition, and
  the unknown-license deny default. No real SBOM or network involved.
- **Artifact Generation / Output Writers**: golden-file/snapshot tests per
  document model × format.
- **CLI**: a small number of end-to-end tests against fixture SBOMs
  covering the primary NuGet + native-library scenario.

## 11. Packaging & distribution

Attributary ships as a .NET tool (`PackAsTool=true`), not a hand-deployed
console app:

- **Package id**: `Attributary`. **Command name** (`ToolCommandName`):
  `attributary`. **Target framework**: `net10.0` — a single, portable,
  framework-dependent package that runs via an installed .NET runtime. The
  primary audience already has one, since they're building the .NET 10 app
  Attributary is generating compliance docs for.
- **Install path**: a repo-committed local tool manifest
  (`dotnet new tool-manifest` + `dotnet tool install --local Attributary`,
  invoked via `dotnet tool run attributary`) is the recommended default —
  it pins the exact tool version in source control alongside the SBOM/config
  it processes, which matters for reproducible CI runs. Global install
  (`dotnet tool install --global Attributary`) is also supported for ad hoc
  developer use.
- **Single nupkg in v1** — no RID-specific builds, to keep the
  build/publish pipeline simple while the core tool is still stabilizing.

**Deferred**: .NET SDK 10 added support for packaging RID-specific,
self-contained, and Native AOT .NET tools within one nupkg — `dotnet tool
install` auto-selects the right asset per platform, and a package can
combine AOT for known RIDs with a portable IL fallback for others. This
would matter if Attributary needs to run somewhere with no .NET runtime at
all — plausible given the native/C/C++ file-share component scenario,
where a build environment might have no .NET tooling installed. Revisit
once the core tool is stable; v1 stays a single portable package.

## 12. Deferred (v2+) ideas

- `SOURCE-OFFER.txt` generation for copyleft `source-offer`-flagged
  components (templated written-offer notice, not actual source bundling).
- Richer CC-BY-style attribution schema for bundled fonts/docs/data.
- Diff/incremental mode: compare a run's output against a previous run to
  highlight newly added/removed components for PR review.
- Container image SBOM scenario (currently: binary/compiled app is the
  primary target; container SBOMs tend to be larger and more
  heterogeneous — revisit once the binary-distribution path is solid).
- Broader ecosystem coverage beyond NuGet + unmanaged/file-share
  components (npm, Maven, PyPI, Cargo, Go modules, OS packages).
- RID-specific/self-contained/NativeAOT tool packaging (.NET SDK 10
  feature) for environments without a .NET runtime installed.
- **Wire multi-license AND-composition end-to-end.** A component genuinely
  under multiple simultaneous licenses — a real SPDX `"X AND Y"` expression,
  or a CycloneDX component with more than one entry in its `licenses[]`
  array (synthesized into an `"X AND Y"`-shaped expression string during
  ingestion, §4) — currently always stops at License Resolution with an
  `ATT2001` diagnostic asking for SBOM enrichment to a single id, the same
  as an unresolved `OR` expression. `RuleMatcher.MatchExpression`
  (union obligations, most-restrictive policy) already exists and is
  tested for exactly this case, but has no caller: `LicenseResolution`
  would need to carry a list of resolved ids instead of one to wire it in,
  which is a real shape change to a type most of the pipeline depends on.
  As multi-licensed components become more common to see in practice, this
  should move from "diagnostic, ask for enrichment" to "actually resolve
  and compose obligations across all of them."
