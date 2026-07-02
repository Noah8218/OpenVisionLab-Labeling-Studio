param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $repoRoot "artifacts\logs"
$uiDir = Join-Path $repoRoot "artifacts\ui"
New-Item -ItemType Directory -Force -Path $logDir, $uiDir | Out-Null

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$buildLog = Join-Path $logDir "verify-wpf-segmentation-objects-build-$stamp.log"
$segmentationTestLog = Join-Path $logDir "verify-wpf-segmentation-objects-tests-$stamp.log"
$visualLog = Join-Path $logDir "verify-wpf-segmentation-objects-visual-$stamp.log"
$visualPng = Join-Path $uiDir "verify-wpf-segmentation-objects-$stamp.png"

Push-Location $repoRoot
try {
    dotnet build .\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj -c $Configuration -m *> $buildLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-segmentation-object-verification *> $segmentationTestLog
    dotnet run --project tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj --configuration $Configuration --no-build -- --wpf-visual-smoke --review-tab=guide --expand-learning-concepts --output="$visualPng" *> $visualLog

    Write-Host "WPF segmentation object verification complete."
    Write-Host "Build log : $buildLog"
    Write-Host "Tests     : $segmentationTestLog"
    Write-Host "Visual log: $visualLog"
    Write-Host "Capture   : $visualPng"
}
finally {
    Pop-Location
}
