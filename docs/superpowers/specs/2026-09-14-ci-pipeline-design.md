# CI Pipeline — Design Spec

Date: 2026-09-14
Status: Approved for planning

## 1. Purpose

This is sub-project 2 of the release-readiness initiative (sub-project 1,
"Release Foundation," is complete and merged to `main`). It gives Attributary
a GitHub Actions CI pipeline built directly on top of sub-project 1's
`./build.ps1` / `./build.sh` entry point: build+test on every push and pull
request, a security-scanning workflow, automated dependency updates, coverage
reporting, and (as a final, separately-approved step) branch protection
requiring the CI check to pass before merging into `main`.

Repo facts this design depends on: the GitHub repo (`mgnslndh/Attributary`)
is public, `gh` CLI is already authenticated as the repo owner, and the
solo maintainer merges their own PRs (so branch protection should not
require a second reviewer).

## 2. Non-goals (this sub-project)

- **No NuGet.org publishing.** That remains sub-project 5, gated on the
  user's separate explicit approval. This CI pipeline packs and uploads the
  `.nupkg`/`.snupkg` as a downloadable workflow artifact only — it never
  runs `dotnet nuget push`.
- **No release automation / tagging workflow.** That's sub-project 4
  (governance docs + release automation), not this one.
- **No required PR review in branch protection.** The maintainer merges
  their own PRs; only the CI status checks are required.
- **Branch protection is not configured until after a real push.** GitHub's
  branch protection API can reference required status-check names before
  they've ever reported, but reliably verifying they're named correctly
  requires the workflow to have actually run once. This sub-project's
  implementation tasks build and locally validate everything else; the
  branch-protection step is deliberately last and requires the user's
  explicit go-ahead to push before it can be verified end-to-end.

## 3. Components

### 3.1 CI workflow (`.github/workflows/ci.yml`)

Triggers: `push` to `main`, and `pull_request` (any branch, targeting `main`
by default via the base-branch filter).

`permissions: contents: read` at the workflow level (least privilege — this
workflow only builds and tests, it doesn't need write access to anything).

One job, `build-and-test`, matrix over `os: [ubuntu-latest, windows-latest]`,
`fail-fast: false` (so a failure on one OS doesn't cancel the other before
it finishes — both results are useful).

Steps, per matrix leg:

1. **Checkout** — `actions/checkout@v7` with `fetch-depth: 0`. This is not
   optional: MinVer (sub-project 1) computes versions from `git describe`
   against the full commit/tag history. A shallow clone (the default) would
   make MinVer see a truncated history and silently compute a wrong height
   or miss the nearest tag entirely.
2. **Setup .NET** — `actions/setup-dotnet@v6` with `global-json-file:
   global.json`. This reads the SDK version from the same `global.json`
   sub-project 1 pinned (`10.0.401`, `rollForward: latestFeature`), so CI
   and local dev can never silently drift onto different SDK versions.
3. **Run the OS-native build script** — `./build.sh` on
   `runner.os != 'Windows'`, `./build.ps1` (via `shell: pwsh`) on
   `runner.os == 'Windows'`. This is deliberate: sub-project 1's Critical
   fixes existed specifically to make `build.sh` actually work on Linux;
   running it for real here — not routing everything through `pwsh`, which
   is also preinstalled on `ubuntu-latest` and would technically "work" — is
   what actually proves that fix holds. No `CI` environment variable needs
   to be set explicitly: GitHub Actions runners already export `CI=true` by
   default, which is exactly what `Directory.Build.props`'s
   `ContinuousIntegrationBuild` condition (sub-project 1, Task 3) was
   written to pick up.
4. **Upload coverage to Codecov** — `codecov/codecov-action@v5`, only on
   the `ubuntu-latest` leg (uploading from both legs would double-count the
   same commit's coverage for no benefit). Pointed at
   `TestResults/**/*.cobertura.xml` — every test project's own per-project
   Cobertura file that `TestTask` (sub-project 1) already produces before
   its own merge step runs. Codecov merges multiple files itself, so
   `TestTask.cs` does not need to change. Requires `token:
   ${{ secrets.CODECOV_TOKEN }}` — Codecov's tokenless upload only applies
   to fork-PR contributions, not a repo's own branches, so this repository
   secret is a hard prerequisite (see §5). `fail_ci_if_error: false` so a
   transient Codecov outage doesn't fail the whole build over something
   that isn't actually a code problem.
5. **Pack** — `./build.sh Pack`, only on the `ubuntu-latest` leg (one
   canonical package build per run is enough; running `Pack` on both legs
   would just produce two identical `.nupkg`s).
6. **Upload package artifact** — `actions/upload-artifact@v7`, only on the
   `ubuntu-latest` leg, uploading `artifacts/*.nupkg` and
   `artifacts/*.snupkg` as a downloadable workflow artifact named
   `nuget-package`. This makes every PR's actual package inspectable
   without needing to publish anything.

### 3.2 CodeQL security scanning (`.github/workflows/codeql.yml`)

Advanced (YAML-defined) setup rather than the dashboard's "default setup"
toggle, so the configuration is version-controlled like everything else in
this repo. Triggers: `push`/`pull_request` to `main`, plus a weekly
schedule (`cron: '30 4 * * 1'`, Monday 04:30 UTC) so scanning doesn't go
stale between pushes.

One job, `analyze`, `runs-on: ubuntu-latest`, `permissions: actions: read,
contents: read, security-events: write` (the minimum CodeQL needs to write
results back to the repo's Security tab). `strategy.matrix.language:
['csharp']` (single language, kept as a matrix for easy extension later).

Steps: checkout (`actions/checkout@v7`), `github/codeql-action/init@v4`
with `languages: csharp`, then an **explicit build** — `actions/setup-dotnet@v6`
(reading `global.json`, same as the CI workflow) followed by `dotnet build
Attributary.sln` — rather than CodeQL's "Autobuild" feature. Autobuild's
build-system detection is heuristic and has a well-known track record of
mishandling less-common MSBuild setups; this repo's central package
management (sub-project 1) is exactly the kind of setup that can trip it
up, so an explicit, known-working build command is more reliable. Finally
`github/codeql-action/analyze@v4` with `category: "/language:csharp"`.

### 3.3 Dependabot (`.github/dependabot.yml`)

Two update configs, both weekly:

```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
```

`nuget` keeps every `PackageVersion` in `Directory.Packages.props` current;
`github-actions` keeps the pinned major versions in `ci.yml`/`codeql.yml`
current as new majors ship. Dependabot opens PRs directly; it needs no
secrets or account setup beyond what's already enabled by default on a
GitHub repo.

### 3.4 README badges

Two badges added directly under the `# Attributary` title in `README.md`:

```markdown
[![CI](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml/badge.svg)](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/mgnslndh/Attributary/graph/badge.svg)](https://codecov.io/gh/mgnslndh/Attributary)
```

Both badges will show as "unknown"/broken until the workflow has run at
least once and (for the coverage badge) the repo has been activated on
codecov.io — expected and self-resolving once §5's manual steps happen and
the first push lands.

## 4. Sequencing and manual prerequisites

Tasks implementing §3.1–§3.4 can be written, committed, and validated
locally (YAML syntax, `dotnet build`/`dotnet format` sanity checks) without
any external action. Two things require the user directly, outside any
task's automation:

1. **Codecov activation** — sign in to codecov.io with the GitHub account
   that owns this repo, activate `mgnslndh/Attributary`, copy its upload
   token, and add it as a GitHub Actions repository secret named
   `CODECOV_TOKEN` (repo Settings → Secrets and variables → Actions → New
   repository secret). Until this exists, the Codecov upload step in
   `ci.yml` will fail or no-op depending on Codecov's current behavior for
   a missing token — `fail_ci_if_error: false` (§3.1) ensures this doesn't
   fail the whole CI run either way.
2. **Pushing `main`** — this session has no push access to
   `git@github.com:mgnslndh/Attributary.git` (confirmed: `git fetch`
   returns a publickey permission error). Local `main` is currently 16+
   commits ahead of `origin/main`. Nothing in this workflow can run on
   GitHub until the user pushes from an environment that has push access.

## 5. Branch protection (final step, separately gated)

After the workflow has run at least once against `main` (i.e., after the
user has pushed and a `build-and-test` run has completed), configure
required status checks via the GitHub API:

```bash
gh api --method PUT repos/mgnslndh/Attributary/branches/main/protection \
  -H "Accept: application/vnd.github+json" \
  -f 'required_status_checks[strict]=true' \
  -f 'required_status_checks[contexts][]=build-and-test (ubuntu-latest)' \
  -f 'required_status_checks[contexts][]=build-and-test (windows-latest)' \
  -F 'enforce_admins=false' \
  -F 'required_pull_request_reviews=null' \
  -F 'restrictions=null'
```

`enforce_admins=false` so the maintainer (a repo admin) isn't blocked by
their own rule in an emergency; `required_pull_request_reviews=null` and
`restrictions=null` mean no review requirement and no push restrictions
beyond the status check itself, per §2's non-goal. The two context strings
match the matrix job's default GitHub-generated check names
(`<job_name> (<matrix.os value>)`); this must be verified against the
actual check names GitHub reports after the first real run before this
step is executed, in case GitHub's naming convention differs from this
prediction.

## 6. Testing

- `.github/workflows/ci.yml` and `.github/workflows/codeql.yml`: validate
  YAML syntax (e.g. `python -c "import yaml, sys; yaml.safe_load(open(sys.argv[1]))"`
  or an actionlint binary if available) before committing — a workflow
  file with a syntax error fails silently from a plain `git commit`
  standpoint and only surfaces once pushed.
- `.github/dependabot.yml`: same YAML-syntax validation.
- README badge markdown: visually confirm it renders as two badge images
  (they'll show broken/unknown status until §4's prerequisites are met,
  which is expected, not a bug).
- End-to-end verification of the workflows actually running successfully
  on GitHub is only possible after the user pushes (§4) — this sub-project
  cannot fully verify itself without that push, and that's an explicit,
  acknowledged limitation, not an oversight.

## 7. Risks / open questions carried forward

- Exact `actions/checkout`, `actions/setup-dotnet`, `actions/upload-artifact`,
  `codecov/codecov-action`, and `github/codeql-action` major versions
  (`v7`, `v6`, `v7`, `v5`, `v4` respectively) were the best available
  information at spec-writing time via web search, which gave inconsistent
  signals for `codecov/codecov-action`'s exact latest major in particular.
  The implementation plan should treat these as strong defaults, not
  immutable requirements — if any action has moved to a newer major by
  implementation time, use the newer one instead of forcing a stale pin.
- The branch-protection check-name prediction in §5
  (`build-and-test (ubuntu-latest)` / `build-and-test (windows-latest)`)
  is GitHub's documented default convention for a matrixed job with no
  explicit `name:` override, but must be confirmed against the real check
  names reported after the first push before running the `gh api` command,
  per §5's own caveat.
