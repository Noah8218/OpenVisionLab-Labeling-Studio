param(
    [switch]$SkipBuild,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
if (-not $SkipBuild) {
        dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c $Configuration -v:minimal -m
    }

    $testAssembly = Join-Path $repoRoot "tests\LabelingApplication.Tests\bin\$Configuration\net8.0-windows\LabelingApplication.Tests.dll"
    if (-not (Test-Path -LiteralPath $testAssembly -PathType Leaf)) {
        throw "Test assembly not found: $testAssembly"
    }

    dotnet $testAssembly --real-yolo-smoke
}
finally {
    Pop-Location
}
