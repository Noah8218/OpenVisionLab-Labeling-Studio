param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoRoot "artifacts\logs"
$uiDir = Join-Path $repoRoot "artifacts\ui"
New-Item -ItemType Directory -Force -Path $logDir, $uiDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$buildLog = Join-Path $logDir "verify-wpf-annotation-objects-build-$stamp.log"
$testLog = Join-Path $logDir "verify-wpf-annotation-objects-focused-tests-$stamp.log"
$visualLog = Join-Path $logDir "verify-wpf-annotation-objects-visual-$stamp.log"
$visualPng = Join-Path $uiDir "verify-wpf-annotation-objects-$stamp.png"

Push-Location $repoRoot
try {
    dotnet build MvcVisionSystem.sln -c $Configuration -m:1 *> $buildLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-annotation-object-verification *> $testLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-visual-smoke --review-tab=guide --expand-learning-concepts --output="$visualPng" *> $visualLog

    Write-Host "WPF annotation object verification complete."
    Write-Host "Build log : $buildLog"
    Write-Host "Test log  : $testLog"
    Write-Host "Visual log: $visualLog"
    Write-Host "Capture   : $visualPng"
}
finally {
    Pop-Location
}
