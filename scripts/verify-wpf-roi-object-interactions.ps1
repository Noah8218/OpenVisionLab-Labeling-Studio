param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoRoot "artifacts\logs"
$uiDir = Join-Path $repoRoot "artifacts\ui"
New-Item -ItemType Directory -Force -Path $logDir, $uiDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$buildLog = Join-Path $logDir "verify-wpf-roi-objects-build-$stamp.log"
$roiTestLog = Join-Path $logDir "verify-wpf-roi-objects-tests-$stamp.log"
$visualLog = Join-Path $logDir "verify-wpf-roi-objects-visual-$stamp.log"
$visualPng = Join-Path $uiDir "verify-wpf-roi-objects-$stamp.png"

Push-Location $repoRoot
try {
    dotnet build MvcVisionSystem.sln -c $Configuration -m:1 *> $buildLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-roi-object-verification *> $roiTestLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-visual-smoke --review-tab=guide --expand-learning-concepts --output="$visualPng" *> $visualLog

    Write-Host "WPF ROI object verification complete."
    Write-Host "Build log : $buildLog"
    Write-Host "ROI tests : $roiTestLog"
    Write-Host "Visual log: $visualLog"
    Write-Host "Capture   : $visualPng"
}
finally {
    Pop-Location
}
