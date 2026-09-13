<#
.SYNOPSIS
    Runs each test project with code coverage and prints a per-package
    line-coverage summary, grouped by which test project produced each result.

.DESCRIPTION
    Each test project is run separately, with its own `--results-directory`, so
    each project's Cobertura file is unambiguous by construction -- no need to
    guess which file came from which project after the fact.

    That per-project isolation matters here specifically: an earlier version of
    this script ran the whole solution in one `dotnet test` and tried to map
    each GUID-named Cobertura file back to its producing test project by
    matching package names against each project's primary (first)
    ProjectReference. That heuristic assumes a package name uniquely identifies
    one test project's file, which breaks down as soon as one test project's
    coverage file also contains packages belonging to another project's SUT --
    exactly what happens with Attributary.Cli.Tests, whose end-to-end pipeline
    tests (GenerateOrchestratorTests, GenerateRunnerTests) exercise the real
    ingestion -> resolution -> rule-engine -> artifact pipeline and so pull in
    Attributary.Sbom/Resolution/Rules/Artifacts/Output too, each of which also
    has its own dedicated unit-test project reporting on the very same
    packages. The heuristic silently mislabeled several rows as a result.
    Running one project at a time avoids the ambiguity entirely instead of
    trying to resolve it after the fact.

    A package can still show different numbers across different rows for that
    reason -- e.g. Attributary.Domain will show once for each test project that
    happens to exercise it. This script does NOT attempt to merge those into
    one "true" combined percentage: Cobertura's <package>/<class> elements only
    carry line-rate ratios, not raw covered/valid line counts, so a
    mathematically correct merge would mean walking down to individual
    <line hits="N"> elements and deduplicating by line number across runs -- a
    solved problem (see `reportgenerator`), not worth hand-rolling here.
    Showing each run's number separately, grouped by test project, is honest
    about what was actually measured and where.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testResultsRoot = Join-Path $repoRoot 'TestResults'

$testProjects = Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Filter '*.csproj' -Recurse
if (-not $testProjects) {
    throw "No test projects found under '$(Join-Path $repoRoot 'tests')'."
}

$rows = [System.Collections.Generic.List[pscustomobject]]::new()
$anyTestsFailed = $false

Push-Location $repoRoot
try {
    foreach ($project in $testProjects) {
        $projectResultsDir = Join-Path $testResultsRoot $project.BaseName

        & dotnet test $project.FullName --coverage --coverage-output-format cobertura --results-directory $projectResultsDir
        if ($LASTEXITCODE -ne 0) {
            $anyTestsFailed = $true
        }

        $coverageFile = Get-ChildItem -Path $projectResultsDir -Filter '*.cobertura.xml' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if (-not $coverageFile) {
            Write-Warning "No .cobertura.xml was produced for '$($project.BaseName)' under '$projectResultsDir'."
            continue
        }

        [xml]$coverageXml = Get-Content -Raw $coverageFile.FullName
        foreach ($package in @($coverageXml.coverage.packages.package)) {
            $rows.Add([pscustomobject]@{
                TestProject = $project.BaseName
                Package     = $package.name
                Coverage    = [double]$package.'line-rate' * 100
            })
        }
    }
}
finally {
    Pop-Location
}

if ($rows.Count -eq 0) {
    throw 'No coverage data was produced by any test project.'
}

Write-Host ''
Write-Host 'Coverage summary:' -ForegroundColor Green
$rows | Sort-Object TestProject, Package | Format-Table -Property `
    @{ Label = 'Test Project'; Expression = { $_.TestProject } }, `
    @{ Label = 'Package'; Expression = { $_.Package } }, `
    @{ Label = 'Coverage'; Expression = { '{0:N2}%' -f $_.Coverage }; Align = 'Right' } `
    -AutoSize

if ($anyTestsFailed) {
    Write-Warning 'One or more test projects had failing tests -- see output above; coverage numbers may not reflect a clean run.'
    exit 1
}
