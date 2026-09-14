# CI Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give Attributary a GitHub Actions CI pipeline (build+test on push/PR across Ubuntu+Windows, CodeQL security scanning, Dependabot, Codecov coverage reporting, README badges) built on top of sub-project 1's `./build.ps1`/`./build.sh` entry point, plus branch protection as a final, separately-gated step.

**Architecture:** Four independent, locally-verifiable tasks (CI workflow, CodeQL workflow, Dependabot config, README badges) followed by one controller-executed task (branch protection) that requires the user's explicit approval to push before it can run for real — pushing to a shared remote is a stop-and-ask action, not something any dispatched implementer subagent should do on its own.

**Tech Stack:** GitHub Actions (`actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/upload-artifact@v7`, `codecov/codecov-action@v5`, `github/codeql-action@v4`), Dependabot, `gh` CLI.

**Spec:** [`docs/superpowers/specs/2026-09-14-ci-pipeline-design.md`](../specs/2026-09-14-ci-pipeline-design.md)

## Global Constraints

- CI workflow triggers: `push` to `main` and `pull_request` targeting `main`.
- OS matrix: `ubuntu-latest`, `windows-latest` only (not macOS).
- `actions/checkout` MUST use `fetch-depth: 0` in both workflows that build the solution — MinVer needs full git history, and a shallow clone silently produces a wrong version rather than failing loudly.
- `actions/setup-dotnet` reads `global-json-file: global.json` — never hardcode an SDK version number in a workflow file.
- Each CI matrix leg runs the OS-native script: `./build.sh` on non-Windows, `./build.ps1` (via `shell: pwsh`) on Windows. Never route both through `pwsh` — the point is to prove `build.sh` itself works on Linux.
- Do not set a `CI` environment variable explicitly anywhere — GitHub Actions runners already export `CI=true` by default, which is what `Directory.Build.props`'s `ContinuousIntegrationBuild` condition (sub-project 1) already reads.
- Codecov upload, `Pack`, and the package-artifact upload run ONLY on the `ubuntu-latest` leg (`if: runner.os == 'Linux'`) — never duplicate these across both matrix legs.
- Codecov needs `secrets.CODECOV_TOKEN` (a real token, not tokenless — tokenless Codecov upload only applies to fork-PR contributions, not this repo's own branches) and `fail_ci_if_error: false`, pointed at `TestResults/**/*.cobertura.xml`.
- CodeQL: advanced/YAML setup (not the dashboard's default-setup toggle), explicit `dotnet build` step instead of Autobuild (central package management can trip up CodeQL's build-system heuristics).
- No task in this plan runs `dotnet nuget push` or any NuGet.org publish command.
- No task in this plan requires a PR review in branch protection — the maintainer merges their own PRs.
- Repo: `mgnslndh/Attributary` (public). Base branch: `main`.
- The exact action major versions above were current at spec-writing time; if any has moved to a newer major by the time a task executes, use the newer one — these are strong defaults, not hard pins.

---

### Task 1: CI workflow

**Files:**
- Create: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: `global.json` (SDK version), `build.ps1`/`build.sh` (sub-project 1's build entry points), `Directory.Build.props`'s `ContinuousIntegrationBuild` property (sub-project 1).
- Produces: a `build-and-test` check per matrix OS (`build-and-test (ubuntu-latest)`, `build-and-test (windows-latest)`) that Task 5 will later require in branch protection; a `nuget-package` workflow artifact per run.

- [ ] **Step 1: Create `.github/workflows/ci.yml`**

```yaml
name: CI

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]

permissions:
  contents: read

jobs:
  build-and-test:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - name: Checkout
        uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      - name: Build (Linux/macOS)
        if: runner.os != 'Windows'
        run: ./build.sh

      - name: Build (Windows)
        if: runner.os == 'Windows'
        shell: pwsh
        run: ./build.ps1

      - name: Upload coverage to Codecov
        if: runner.os == 'Linux'
        uses: codecov/codecov-action@v5
        with:
          token: ${{ secrets.CODECOV_TOKEN }}
          files: TestResults/**/*.cobertura.xml
          fail_ci_if_error: false

      - name: Pack
        if: runner.os == 'Linux'
        run: ./build.sh Pack

      - name: Upload package artifact
        if: runner.os == 'Linux'
        uses: actions/upload-artifact@v7
        with:
          name: nuget-package
          path: |
            artifacts/*.nupkg
            artifacts/*.snupkg
```

- [ ] **Step 2: Validate YAML syntax**

Run: `python -m pip install --quiet pyyaml` then:

```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml')); print('OK')"
```

Expected: prints `OK` with no exception. This only validates YAML *syntax*
(indentation, quoting, structure) — it cannot validate GitHub Actions
*semantics* (a typo'd action name, an invalid expression). Full semantic
validation only happens once this runs on GitHub after a push (Task 5's
prerequisite), which this task cannot trigger.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add GitHub Actions build+test workflow"
```

---

### Task 2: CodeQL workflow

**Files:**
- Create: `.github/workflows/codeql.yml`

**Interfaces:**
- Consumes: `global.json` (SDK version), `Attributary.sln`.
- Produces: security scanning results visible under the repo's Security →
  Code scanning tab (only after this runs on GitHub).

- [ ] **Step 1: Create `.github/workflows/codeql.yml`**

```yaml
name: "CodeQL"

on:
  push:
    branches: [ "main" ]
  pull_request:
    branches: [ "main" ]
  schedule:
    - cron: '30 4 * * 1'

jobs:
  analyze:
    name: Analyze
    runs-on: ubuntu-latest
    permissions:
      actions: read
      contents: read
      security-events: write

    strategy:
      fail-fast: false
      matrix:
        language: [ 'csharp' ]

    steps:
      - name: Checkout repository
        uses: actions/checkout@v7
        with:
          fetch-depth: 0

      - name: Setup .NET
        uses: actions/setup-dotnet@v6
        with:
          global-json-file: global.json

      - name: Initialize CodeQL
        uses: github/codeql-action/init@v4
        with:
          languages: ${{ matrix.language }}

      - name: Build
        run: dotnet build Attributary.sln

      - name: Perform CodeQL Analysis
        uses: github/codeql-action/analyze@v4
        with:
          category: "/language:${{matrix.language}}"
```

(`fetch-depth: 0` here too — `dotnet build` on this solution triggers
MinVer the same way `./build.sh`/`./build.ps1` do, via `Directory.Build.props`.)

- [ ] **Step 2: Validate YAML syntax**

Run:

```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/codeql.yml')); print('OK')"
```

Expected: prints `OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/codeql.yml
git commit -m "ci: add CodeQL security scanning workflow"
```

---

### Task 3: Dependabot configuration

**Files:**
- Create: `.github/dependabot.yml`

- [ ] **Step 1: Create `.github/dependabot.yml`**

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

- [ ] **Step 2: Validate YAML syntax**

Run:

```bash
python -c "import yaml; yaml.safe_load(open('.github/dependabot.yml')); print('OK')"
```

Expected: prints `OK`.

- [ ] **Step 3: Commit**

```bash
git add .github/dependabot.yml
git commit -m "ci: add Dependabot config for nuget and github-actions ecosystems"
```

---

### Task 4: README badges

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Add CI and Codecov badges under the title**

Current content (first 3 lines):

```markdown
# Attributary

Attributary is a .NET CLI tool that reads a [CycloneDX](https://cyclonedx.org/) SBOM and generates the artifacts you need to be open source license compliant when distributing a piece of software: per-license license files, an aggregated NOTICE file, a third-party attribution document, and a compliance report — in text, Markdown, JSON, or HTML.
```

New content:

```markdown
# Attributary

[![CI](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml/badge.svg)](https://github.com/mgnslndh/Attributary/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/mgnslndh/Attributary/graph/badge.svg)](https://codecov.io/gh/mgnslndh/Attributary)

Attributary is a .NET CLI tool that reads a [CycloneDX](https://cyclonedx.org/) SBOM and generates the artifacts you need to be open source license compliant when distributing a piece of software: per-license license files, an aggregated NOTICE file, a third-party attribution document, and a compliance report — in text, Markdown, JSON, or HTML.
```

- [ ] **Step 2: Verify**

Run: `git diff README.md` and confirm only the two badge lines (plus one
blank line) were inserted — no other content changed.

Note: both badges will render as "unknown"/broken until the workflow has
actually run on GitHub at least once (CI badge) and the repo has been
activated on codecov.io with `CODECOV_TOKEN` configured (coverage badge).
This is expected — do not treat a broken-looking badge as a bug to
investigate further within this task.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add CI and coverage badges to README"
```

---

### Task 5: Branch protection (controller-executed, gated on user approval)

**This task is different from Tasks 1–4 and must NOT be dispatched to an
implementer subagent as a normal task.** It requires pushing to a shared
remote branch — an action that always requires the user's explicit,
in-the-moment approval, not something authorized by this plan having been
approved earlier. Whoever is executing this plan (a controller session
running subagent-driven-development, or a human) must perform this task's
steps directly, stopping to ask before the push.

**Files:** none (no repo files change in this task; it's a GitHub API call).

- [ ] **Step 1: Confirm Tasks 1–4 are committed, then ask the user for explicit permission to push `main`**

Local `main` will be some number of commits ahead of `origin/main` at this
point (sub-project 1's 16 commits plus Tasks 1–4 above). Ask the user
directly: "Ready to push `main` to `origin` so the new CI workflows can run
for real? This is needed before branch protection can be configured
against verified check names." Do not proceed to Step 2 without an
explicit yes.

- [ ] **Step 2: Push**

Run: `git push origin main`

- [ ] **Step 3: Wait for the CI workflow to complete, then read its actual check names**

Run: `gh run list --branch main --limit 5` to find the `build-and-test`
run triggered by the push, then `gh run view <run-id> --json jobs
--jq '.jobs[].name'` to print the exact job names GitHub reports. Compare
these against the predicted names `build-and-test (ubuntu-latest)` and
`build-and-test (windows-latest)` (§5 of the spec already flags this as a
prediction to verify, not a certainty). If the real names differ, use the
real ones in Step 4.

- [ ] **Step 4: Configure branch protection**

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

(Substitute the real check names from Step 3 if they differed from the
prediction.)

- [ ] **Step 5: Verify**

Run: `gh api repos/mgnslndh/Attributary/branches/main/protection --jq '.required_status_checks.contexts'`
Expected: prints an array containing both check-name strings actually used
in Step 4.

- [ ] **Step 6: Report to the user**

Tell the user: branch protection is live on `main`, requiring both CI
matrix legs to pass before a PR can merge; no PR review is required since
they merge their own PRs. Also remind them that the Codecov badge/upload
still needs `CODECOV_TOKEN` added as a repository secret (spec §4) if they
haven't done that yet — this task does not create that secret, since it
requires signing in to codecov.io, an account action only the user can do.

---

## Self-Review Notes

- **Spec coverage:** §3.1 (CI workflow) → Task 1. §3.2 (CodeQL) → Task 2.
  §3.3 (Dependabot) → Task 3. §3.4 (badges) → Task 4. §5 (branch
  protection) → Task 5. §4's two manual prerequisites (Codecov activation,
  pushing `main`) are surfaced explicitly in Task 5 rather than silently
  assumed.
- **Why Task 5 is controller-executed, not subagent-dispatched:** pushing
  to a shared remote is one of subagent-driven-development's four
  hard-stop conditions ("a side effect outside this worktree that norms
  say you ask about first"). An implementer subagent should never be
  given `git push origin main` as a step to run autonomously; the
  controller session (or a human) must do it after asking, in the moment,
  regardless of what this plan or its earlier approval said.
- **YAML validation is syntax-only, not semantic:** Tasks 1–3's Step 2
  checks catch indentation/structure errors, not GitHub-Actions-specific
  mistakes (an invalid `uses:` reference, a bad expression). This is
  called out explicitly in Task 1 so nobody mistakes a passing YAML-syntax
  check for proof the workflow actually works — that only happens after
  Task 5's push.
