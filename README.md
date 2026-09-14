# Attributary

[![CI](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml/badge.svg)](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/mgnslndh/Attributary/graph/badge.svg)](https://codecov.io/gh/mgnslndh/Attributary)

Attributary is a .NET CLI tool that reads a [CycloneDX](https://cyclonedx.org/) SBOM and generates the artifacts you need to be open source license compliant when distributing a piece of software: per-license license files, an aggregated NOTICE file, a third-party attribution document, and a compliance report — in text, Markdown, JSON, or HTML.

It's driven by a rule engine you can configure: for each license it knows about, the rules say what must be produced (a copyright line, the license text, a NOTICE entry) and what policy applies (allow it, warn about it, or deny it outright). Attributary ships with a bundled default rule set covering ~20 common OSS licenses, and you can layer your own rules on top.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

## Install

As a local tool, pinned in your repo (recommended — reproducible in CI):

```bash
dotnet new tool-manifest   # first time only, per repo
dotnet tool install --local Attributary
dotnet tool run attributary -- generate --sbom sbom.cdx.json --out ./compliance
```

Or as a global tool:

```bash
dotnet tool install --global Attributary
attributary generate --sbom sbom.cdx.json --out ./compliance
```

## Quick start

```bash
attributary generate --sbom sbom.cdx.json --out ./compliance
```

This reads `sbom.cdx.json` (any CycloneDX JSON BOM), resolves each component's license and copyright, checks them against the rule engine, and — if nothing is denied or unresolved — writes:

```
compliance/
  LICENSES/
    MIT.txt
    Apache-2.0.txt
  NOTICE.txt                  # only written if a component actually requires one
  THIRD-PARTY-NOTICES.txt
  COMPLIANCE-REPORT.txt
```

Diagnostics print as they're found, in an MSBuild-style format (`ATT3001 error: License 'GPL-3.0-only' is denied by policy. [SomePackage 1.2.3]`), and the run exits non-zero if any error-level diagnostic fired.

## Commands

### `generate`

| Flag | Default | Description |
|---|---|---|
| `--sbom <PATH>` | *(required)* | Path to a CycloneDX JSON SBOM. |
| `--out <DIR>` | `./compliance` | Output directory. |
| `--format <FORMATS>` | `txt` | Comma-separated: `txt`, `md`, `json`, `html`. |
| `--group-by <MODE>` | `license` | `license` or `component` — how the attribution/report documents are grouped. |
| `--no-embed-license-text` | off | Reference `LICENSES/<id>.txt` instead of embedding full license text in the attribution document. |
| `--dry-run` | off | Run the full pipeline and report diagnostics, but write nothing to disk. |
| `--fail-fast` | off | Stop at the first error instead of collecting every component's diagnostics first. |
| `--no-cache` | off | Bypass the license-text cache for this run. |
| `--cache-dir <PATH>` | OS-standard cache dir | Override the cache location. |
| `--config <PATH>` | *(none)* | A rules file to layer on top of (or replace rules from) the bundled defaults — see [Configuring rules](#configuring-rules). |
| `--warnaserror` | off | Promote every warning to an error. |
| `--warnaserror-codes <CODES>` | *(none)* | Promote specific diagnostic codes to errors. |
| `--warnaserror-exempt <CODES>` | *(none)* | Exempt specific codes from `--warnaserror`. |
| `--nowarn <CODES>` | *(none)* | Suppress specific diagnostic codes entirely. |
| `--severity <CODE=Level,...>` | *(none)* | Set an exact severity (`Info`/`Warning`/`Error`) per code. |

### `license list` / `license show`

```bash
attributary license list --sbom sbom.cdx.json      # every license in the SBOM, its policy, and whether it's a custom rule
attributary license show Apache-2.0                 # what the rule engine requires for one license id, no SBOM needed
```

### `cache clear` / `cache list` / `cache path`

Manage the on-disk license-text cache (`--cache-dir <PATH>` overrides the location for any of these, same as `generate`).

### `init`

```bash
attributary init                    # writes attributary.config.yaml, seeded with the bundled default rules
attributary init --out my-rules.yaml
```

Refuses to overwrite an existing file.

## Configuring rules

A rules file has a `defaults.unknownLicense` entry (the policy applied to any license the rule engine doesn't otherwise recognize) and a `rules` list, one entry per license id:

```yaml
defaults:
  unknownLicense:
    policy: deny                  # allow | warn | deny
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

  - id: GPL-3.0-*        # trailing * matches a version family, e.g. GPL-3.0-only
    policy: warn
    require: [copyright, license-text]
    flags: [source-offer, copyleft-strong]
```

Run `attributary init` to get a starting file seeded with the full bundled default set, then edit it and pass it back in with `--config`. Rules you list override the bundled rule with the same `id`; anything you don't mention falls back to the bundled default.

## How license/copyright text gets resolved

For each component, Attributary tries, in order: text already embedded in the SBOM (declared license text, or a `license`-type external reference URL) → the local NuGet package cache (for `pkg:nuget/...` components) → the component's VCS repository on GitHub → a bundled offline SPDX license-text dataset. Whatever resolves first for a given field wins; results are cached on disk so repeat runs don't re-hit the network.

If nothing resolves what a license's rule requires, that's a diagnostic, not a guess — the fix is almost always enriching the SBOM (adding a `copyright` field, a VCS URL, etc.) rather than anything Attributary can infer on its own.

## More detail

The full design — the rule engine's data model, the license-resolution fallback chain, the caching scheme, the diagnostic code ranges — is written up in [`docs/superpowers/specs/2026-09-12-attributary-design.md`](docs/superpowers/specs/2026-09-12-attributary-design.md).

## License

MIT — see [`LICENSE`](LICENSE).
