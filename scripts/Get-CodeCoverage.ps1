<#
.SYNOPSIS
    Runs every test project with code coverage and prints the true, merged
    per-assembly line-coverage total.

.DESCRIPTION
    Each test project is run separately, with its own `--results-directory`,
    so each project's Cobertura file is unambiguous by construction. That
    isolation matters here specifically: an earlier version of this script ran
    the whole solution in one `dotnet test` and tried to map each GUID-named
    Cobertura file back to its producing test project by matching package
    names against each project's primary (first) ProjectReference. That
    heuristic assumes a package name uniquely identifies one test project's
    file, which breaks down as soon as one test project's coverage file also
    contains packages belonging to another project's SUT -- exactly what
    happens with Attributary.Cli.Tests, whose end-to-end pipeline tests
    (GenerateOrchestratorTests, GenerateRunnerTests) exercise the real
    ingestion -> resolution -> rule-engine -> artifact pipeline and so pull in
    Attributary.Sbom/Resolution/Rules/Artifacts/Output too, each of which also
    has its own dedicated unit-test project reporting on the very same
    packages. Running one project at a time avoids that ambiguity entirely.

    An even earlier version then printed one row per (test project, package)
    pair -- e.g. Attributary.Domain showing up under five different test
    projects at five different percentages, none of which answers "how much
    of Attributary.Domain is actually covered." That IS answerable correctly,
    though: every test run instruments the same compiled assembly (same
    source, same build), so the set of instrumentable lines for a given class
    is identical in every Cobertura report that mentions it -- only *which* of
    those lines got hit differs per run. So the correct total is: for every
    source line, was it hit in *any* run? `reportgenerator` does exactly this
    kind of merge (and handles branch coverage, multi-framework targets, etc.
    correctly along the way), so this script hands it all 7 Cobertura files
    and prints its per-assembly TextSummary instead of hand-rolling the
    line-level union itself.
#>

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$testResultsRoot = Join-Path $repoRoot 'TestResults'
$mergedReportDir = Join-Path $testResultsRoot 'merged'

$testProjects = Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Filter '*.csproj' -Recurse
if (-not $testProjects) {
    throw "No test projects found under '$(Join-Path $repoRoot 'tests')'."
}

$coverageFiles = [System.Collections.Generic.List[string]]::new()
$anyTestsFailed = $false

Push-Location $repoRoot
try {
    dotnet tool restore | Out-Null

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

        $coverageFiles.Add($coverageFile.FullName)
    }

    if ($coverageFiles.Count -eq 0) {
        throw 'No coverage data was produced by any test project.'
    }

    if (Test-Path $mergedReportDir) {
        Remove-Item -Path $mergedReportDir -Recurse -Force
    }

    & dotnet tool run reportgenerator -- `
        "-reports:$($coverageFiles -join ';')" `
        "-targetdir:$mergedReportDir" `
        '-reporttypes:TextSummary'
}
finally {
    Pop-Location
}

$summaryFile = Join-Path $mergedReportDir 'Summary.txt'
if (-not (Test-Path $summaryFile)) {
    throw "reportgenerator did not produce a summary at '$summaryFile'."
}

Write-Host ''
Get-Content -Raw $summaryFile

if ($anyTestsFailed) {
    Write-Warning 'One or more test projects had failing tests -- see output above; coverage numbers may not reflect a clean run.'
    exit 1
}
