param(
    [string]$PythonExe = "",
    [string]$YoloProjectRoot = "",
    [string]$YoloSourceRoot = "",
    [string]$DataYaml = "",
    [string]$BaselineWeights = "",
    [string]$CandidateWeights = "",
    [int]$ImageSize = 320,
    [int]$BatchSize = 16,
    [ValidateSet("val", "test")]
    [string]$Task = "val",
    [double]$UiConfidence = 0.25,
    [string]$OutputDirectory = "artifacts\yolo-model-comparison"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$repoParent = Split-Path -Parent $repoRoot

function Resolve-PathValue([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $expanded = $Path.Replace('${repoRoot}', $repoRoot).Replace('${repoParent}', $repoParent)
    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return [System.IO.Path]::GetFullPath($expanded)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $expanded))
}

function Assert-File([string]$Path, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name not found: $Path"
    }
}

function Assert-Directory([string]$Path, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Name not found: $Path"
    }
}

function Find-LatestBestWeights([string]$ProjectRoot) {
    $runsRoot = Join-Path $ProjectRoot "runs\train"
    if (-not (Test-Path -LiteralPath $runsRoot -PathType Container)) {
        return ""
    }

    $weights = Get-ChildItem -LiteralPath $runsRoot -Recurse -File -Filter "best.pt" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $weights) {
        return ""
    }

    return $weights.FullName
}

function Invoke-YoloVal([string]$RunName, [string]$WeightsPath, [string]$RunOutputRoot) {
    $logPath = Join-Path $RunOutputRoot "$RunName.log"
    $runProject = Join-Path $RunOutputRoot "runs"
    New-Item -ItemType Directory -Force -Path $runProject | Out-Null

    $arguments = @(
        (Join-Path $YoloSourceRoot "val.py"),
        "--weights", $WeightsPath,
        "--data", $DataYaml,
        "--img", $ImageSize.ToString(),
        "--batch-size", $BatchSize.ToString(),
        "--task", $Task,
        "--workers", "0",
        "--project", $runProject,
        "--name", $RunName,
        "--exist-ok",
        "--save-txt",
        "--save-conf"
    )

    $previous = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    try {
        $ErrorActionPreference = "Continue"
        $output = & $PythonExe @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | Set-Content -LiteralPath $logPath -Encoding UTF8
        if ($exitCode -ne 0) {
            throw "YOLO val failed for $RunName. See $logPath"
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = $previous
    }

    return [ordered]@{
        name = $RunName
        weights = $WeightsPath
        logPath = $logPath
        labelsPath = Join-Path $runProject "$RunName\labels"
        metrics = Read-ValMetrics $logPath
        confidence = Read-PredictionConfidenceSummary (Join-Path $runProject "$RunName\labels") $UiConfidence
    }
}

function Read-ValMetrics([string]$LogPath) {
    $text = Get-Content -LiteralPath $LogPath -Raw
    $pattern = '(?m)^\s*all\s+\d+\s+\d+\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        return [ordered]@{ precision = $null; recall = $null; map50 = $null; map5095 = $null }
    }

    return [ordered]@{
        precision = [double]$match.Groups[1].Value
        recall = [double]$match.Groups[2].Value
        map50 = [double]$match.Groups[3].Value
        map5095 = [double]$match.Groups[4].Value
    }
}

function Read-PredictionConfidenceSummary([string]$LabelsPath, [double]$Threshold) {
    $total = 0
    $aboveThreshold = 0
    $maxConfidence = 0.0
    if (Test-Path -LiteralPath $LabelsPath -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $LabelsPath -File -Filter "*.txt") {
            foreach ($line in Get-Content -LiteralPath $file.FullName) {
                $parts = $line -split '\s+'
                if ($parts.Length -lt 6) {
                    continue
                }

                $confidence = [double]::Parse($parts[5], [System.Globalization.CultureInfo]::InvariantCulture)
                $total++
                if ($confidence -ge $Threshold) {
                    $aboveThreshold++
                }

                if ($confidence -gt $maxConfidence) {
                    $maxConfidence = $confidence
                }
            }
        }
    }

    return [ordered]@{
        predictionCount = $total
        uiCandidateCount = $aboveThreshold
        uiConfidence = $Threshold
        maxConfidence = $maxConfidence
    }
}

function Format-NullableNumber($Value) {
    if ($null -eq $Value) {
        return "-"
    }

    return ([double]$Value).ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture)
}

function Write-MarkdownReport($Summary, [string]$Path) {
    $baseline = $Summary.baseline
    $candidate = $Summary.candidate
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# YOLO Model Comparison")
    $lines.Add("")
    $lines.Add("- Data: ``$($Summary.dataYaml)``")
    $lines.Add("- Task: ``$($Summary.task)``")
    $lines.Add("- UI confidence: ``$($Summary.uiConfidence)``")
    $lines.Add("")
    $lines.Add("| Model | Precision | Recall | mAP50 | mAP50-95 | UI candidates | Max confidence |")
    $lines.Add("| --- | ---: | ---: | ---: | ---: | ---: | ---: |")
    foreach ($item in @($baseline, $candidate)) {
        $metrics = $item.metrics
        $confidence = $item.confidence
        $lines.Add("| $($item.name) | $(Format-NullableNumber $metrics.precision) | $(Format-NullableNumber $metrics.recall) | $(Format-NullableNumber $metrics.map50) | $(Format-NullableNumber $metrics.map5095) | $($confidence.uiCandidateCount)/$($confidence.predictionCount) | $(Format-NullableNumber $confidence.maxConfidence) |")
    }
    $lines.Add("")
    $lines.Add("## Files")
    $lines.Add("")
    $lines.Add("- Baseline weights: ``$($baseline.weights)``")
    $lines.Add("- Candidate weights: ``$($candidate.weights)``")
    $lines.Add("- Baseline log: ``$($baseline.logPath)``")
    $lines.Add("- Candidate log: ``$($candidate.logPath)``")
    $lines | Set-Content -LiteralPath $Path -Encoding UTF8
}

if ([string]::IsNullOrWhiteSpace($YoloProjectRoot)) { $YoloProjectRoot = Join-Path $repoParent "yolov5" }
if ([string]::IsNullOrWhiteSpace($YoloSourceRoot)) { $YoloSourceRoot = Join-Path $YoloProjectRoot "yolov5Master" }
if ([string]::IsNullOrWhiteSpace($PythonExe)) { $PythonExe = Join-Path $YoloProjectRoot ".venv\Scripts\python.exe" }
if ([string]::IsNullOrWhiteSpace($DataYaml)) { $DataYaml = Join-Path $repoRoot "artifacts\yolo_compare_data_20260622.yaml" }
if ([string]::IsNullOrWhiteSpace($BaselineWeights)) { $BaselineWeights = Join-Path $YoloProjectRoot "best.pt" }
if ([string]::IsNullOrWhiteSpace($CandidateWeights)) { $CandidateWeights = Find-LatestBestWeights $YoloProjectRoot }

$PythonExe = Resolve-PathValue $PythonExe
$YoloProjectRoot = Resolve-PathValue $YoloProjectRoot
$YoloSourceRoot = Resolve-PathValue $YoloSourceRoot
$DataYaml = Resolve-PathValue $DataYaml
$BaselineWeights = Resolve-PathValue $BaselineWeights
$CandidateWeights = Resolve-PathValue $CandidateWeights
$OutputDirectory = Resolve-PathValue $OutputDirectory

Assert-File $PythonExe "Python executable"
Assert-Directory $YoloSourceRoot "YOLO source root"
Assert-File (Join-Path $YoloSourceRoot "val.py") "YOLO val.py"
Assert-File $DataYaml "YOLO data.yaml"
Assert-File $BaselineWeights "Baseline weights"
Assert-File $CandidateWeights "Candidate weights"

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runOutputRoot = Join-Path $OutputDirectory $stamp
New-Item -ItemType Directory -Force -Path $runOutputRoot | Out-Null

$baseline = Invoke-YoloVal "baseline" $BaselineWeights $runOutputRoot
$candidate = Invoke-YoloVal "candidate" $CandidateWeights $runOutputRoot

$summary = [ordered]@{
    createdAt = (Get-Date).ToString("o")
    dataYaml = $DataYaml
    task = $Task
    uiConfidence = $UiConfidence
    baseline = $baseline
    candidate = $candidate
}

$jsonPath = Join-Path $runOutputRoot "comparison-summary.json"
$markdownPath = Join-Path $runOutputRoot "comparison-report.md"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
Write-MarkdownReport $summary $markdownPath

Write-Host "[OK] YOLO comparison complete"
Write-Host "[OK] Summary: $jsonPath"
Write-Host "[OK] Report: $markdownPath"
