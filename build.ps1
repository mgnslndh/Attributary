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
