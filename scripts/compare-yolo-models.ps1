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
    [ValidateSet("detect", "segment")]
    [string]$ModelTask = "detect",
    [double]$UiConfidence = 0.25,
    [string]$OutputDirectory = "artifacts\yolo-model-comparison"
)

$ErrorActionPreference = "Stop"

$RecommendedPromotionLabelCount = 10

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

function Test-YoloV5SourceRoot() {
    return Test-Path -LiteralPath (Join-Path $YoloSourceRoot "val.py") -PathType Leaf
}

function Test-UltralyticsSourceRoot() {
    return Test-Path -LiteralPath (Join-Path $YoloSourceRoot "ultralytics") -PathType Container
}

function Assert-YoloValidationRuntime() {
    if ((Test-YoloV5SourceRoot) -or (Test-UltralyticsSourceRoot)) {
        return
    }

    throw "YOLO validation runtime not found: expected val.py or ultralytics package under $YoloSourceRoot"
}

function Read-DataYamlClassCount([string]$Path) {
    $text = Get-Content -LiteralPath $Path -Raw
    $match = [regex]::Match($text, '(?m)^\s*nc\s*:\s*(\d+)\s*$')
    if (-not $match.Success) {
        throw "Model comparison cannot start: label count was not found in the dataset settings."
    }

    return [int]$match.Groups[1].Value
}

function Remove-YamlInlineComment([string]$Value) {
    if ($null -eq $Value) {
        return ""
    }

    $commentIndex = $Value.IndexOf("#")
    if ($commentIndex -ge 0) {
        return $Value.Substring(0, $commentIndex)
    }

    return $Value
}

function Read-DataYamlScalarValues([string]$Path) {
    $values = @{}
    foreach ($rawLine in Get-Content -LiteralPath $Path) {
        $line = (Remove-YamlInlineComment $rawLine).Trim()
        if ([string]::IsNullOrWhiteSpace($line)) {
            continue
        }

        $separatorIndex = $line.IndexOf(":")
        if ($separatorIndex -le 0) {
            continue
        }

        $key = $line.Substring(0, $separatorIndex).Trim()
        $value = $line.Substring($separatorIndex + 1).Trim()
        if ($value.Length -ge 2 -and (($value[0] -eq '"' -and $value[$value.Length - 1] -eq '"') -or ($value[0] -eq "'" -and $value[$value.Length - 1] -eq "'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if (-not [string]::IsNullOrWhiteSpace($key)) {
            $values[$key] = $value.Trim()
        }
    }

    return $values
}

function Resolve-DataYamlValuePath([string]$YamlFilePath, [string]$YamlRootPath, [string]$YamlPath) {
    $normalizedYamlPath = if ($null -eq $YamlPath) { "" } else { $YamlPath }
    $normalizedYamlPath = $normalizedYamlPath.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::IsPathRooted($normalizedYamlPath)) {
        return [System.IO.Path]::GetFullPath($normalizedYamlPath)
    }

    $yamlDirectory = Split-Path -Parent $YamlFilePath
    $root = if ([string]::IsNullOrWhiteSpace($YamlRootPath)) { $yamlDirectory } else { $YamlRootPath.Replace("/", [System.IO.Path]::DirectorySeparatorChar) }
    if (-not [System.IO.Path]::IsPathRooted($root)) {
        $root = Join-Path $yamlDirectory $root
    }

    return [System.IO.Path]::GetFullPath((Join-Path $root $normalizedYamlPath))
}

function Test-SupportedImagePath([string]$Path) {
    $extension = [System.IO.Path]::GetExtension($Path)
    return @(".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff") -contains $extension.ToLowerInvariant()
}

function Resolve-ListImagePath([string]$ListDirectory, [string]$ImagePath) {
    $normalized = if ($null -eq $ImagePath) { "" } else { $ImagePath }
    $normalized = $normalized.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::IsPathRooted($normalized)) {
        return [System.IO.Path]::GetFullPath($normalized)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ListDirectory $normalized))
}

function Resolve-LabelPathFromImagePath([string]$ImagePath) {
    $directory = Split-Path -Parent $ImagePath
    $directoryName = Split-Path -Leaf $directory
    $parent = Split-Path -Parent $directory
    $labelsDirectory = if ($directoryName -ieq "images" -and -not [string]::IsNullOrWhiteSpace($parent)) {
        Join-Path $parent "labels"
    } else {
        Join-Path $directory "labels"
    }

    return Join-Path $labelsDirectory ([System.IO.Path]::GetFileNameWithoutExtension($ImagePath) + ".txt")
}

function Get-SplitImagePaths([string]$ResolvedPath) {
    if ([string]::IsNullOrWhiteSpace($ResolvedPath)) {
        return @()
    }

    if (Test-Path -LiteralPath $ResolvedPath -PathType Container) {
        return @(Get-ChildItem -LiteralPath $ResolvedPath -File -ErrorAction SilentlyContinue |
            Where-Object { Test-SupportedImagePath $_.FullName } |
            ForEach-Object { $_.FullName })
    }

    if (Test-Path -LiteralPath $ResolvedPath -PathType Leaf) {
        $listDirectory = Split-Path -Parent $ResolvedPath
        return @(Get-Content -LiteralPath $ResolvedPath |
            ForEach-Object { (Remove-YamlInlineComment $_).Trim() } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            ForEach-Object { Resolve-ListImagePath $listDirectory $_ } |
            Where-Object { (Test-Path -LiteralPath $_ -PathType Leaf) -and (Test-SupportedImagePath $_) })
    }

    return @()
}

function Test-SegmentationLabelLine([string]$Line) {
    $text = if ($null -eq $Line) { "" } else { $Line }
    $tokens = @($text -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($tokens.Count -lt 7 -or $tokens.Count % 2 -eq 0) {
        return $false
    }

    foreach ($token in $tokens) {
        $value = 0.0
        if (-not [double]::TryParse($token, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$value)) {
            return $false
        }
    }

    return $true
}

function New-ComparisonEvidence([string]$DataYamlPath, [string]$SplitName, [string]$ValidationTask) {
    $values = Read-DataYamlScalarValues $DataYamlPath
    $splitPath = if ($values.ContainsKey($SplitName)) { $values[$SplitName] } else { "" }
    $yamlRootPath = if ($values.ContainsKey("path")) { $values["path"] } else { "" }
    $resolvedSplitPath = if ([string]::IsNullOrWhiteSpace($splitPath)) { "" } else { Resolve-DataYamlValuePath $DataYamlPath $yamlRootPath $splitPath }
    $imagePaths = @(Get-SplitImagePaths $resolvedSplitPath)
    $labelPaths = @($imagePaths |
        ForEach-Object { Resolve-LabelPathFromImagePath $_ } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $positiveSegmentationLabelLineCount = if ($ValidationTask -ieq "segment") {
        @($labelPaths |
            ForEach-Object { Get-Content -LiteralPath $_ } |
            ForEach-Object { (Remove-YamlInlineComment $_).Trim() } |
            Where-Object { Test-SegmentationLabelLine $_ }).Count
    } else {
        $null
    }

    return [ordered]@{
        split = $SplitName
        imagesPath = $resolvedSplitPath
        imageCount = $imagePaths.Count
        labelFileCount = $labelPaths.Count
        comparisonLabelCount = [System.Math]::Min($imagePaths.Count, $labelPaths.Count)
        positiveSegmentationLabelLineCount = $positiveSegmentationLabelLineCount
        recommendedLabelCount = $RecommendedPromotionLabelCount
    }
}

function Convert-YamlClassNameScalar([string]$Value) {
    $raw = if ($null -eq $Value) { "" } else { $Value }
    $name = (Remove-YamlInlineComment $raw).Trim().TrimEnd(",")
    $mapMatch = [regex]::Match($name, '^\s*[^:]+:\s*(.+)$')
    if ($mapMatch.Success) {
        $name = $mapMatch.Groups[1].Value.Trim()
    }

    if ($name.Length -ge 2 -and (($name[0] -eq '"' -and $name[$name.Length - 1] -eq '"') -or ($name[0] -eq "'" -and $name[$name.Length - 1] -eq "'"))) {
        $name = $name.Substring(1, $name.Length - 2)
    }

    return $name.Trim()
}

function Split-InlineClassNames([string]$Value, [string]$Open, [string]$Close) {
    $raw = if ($null -eq $Value) { "" } else { $Value }
    $trimmed = (Remove-YamlInlineComment $raw).Trim()
    if ($trimmed.StartsWith($Open) -and $trimmed.EndsWith($Close)) {
        $trimmed = $trimmed.Substring(1, $trimmed.Length - 2)
    }

    return @($trimmed -split "," | ForEach-Object { Convert-YamlClassNameScalar $_ } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Read-DataYamlClassNames([string]$Path) {
    $lines = @(Get-Content -LiteralPath $Path)
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = (Remove-YamlInlineComment $lines[$index]).Trim()
        $match = [regex]::Match($line, '^names\s*:\s*(.*)$')
        if (-not $match.Success) {
            continue
        }

        $value = $match.Groups[1].Value.Trim()
        if ($value.StartsWith("[") -and $value.EndsWith("]")) {
            return @(Split-InlineClassNames $value "[" "]")
        }

        if ($value.StartsWith("{") -and $value.EndsWith("}")) {
            return @(Split-InlineClassNames $value "{" "}")
        }

        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return @(Convert-YamlClassNameScalar $value)
        }

        $names = New-Object System.Collections.Generic.List[string]
        for ($itemIndex = $index + 1; $itemIndex -lt $lines.Count; $itemIndex++) {
            $itemLine = Remove-YamlInlineComment $lines[$itemIndex]
            $trimmed = $itemLine.Trim()
            if ([string]::IsNullOrWhiteSpace($trimmed)) {
                continue
            }

            $listMatch = [regex]::Match($itemLine, '^\s*-\s*(.+)$')
            $mapMatch = [regex]::Match($itemLine, '^\s*\d+\s*:\s*(.+)$')
            if ($listMatch.Success) {
                $names.Add((Convert-YamlClassNameScalar $listMatch.Groups[1].Value))
                continue
            }

            if ($mapMatch.Success) {
                $names.Add((Convert-YamlClassNameScalar $mapMatch.Groups[1].Value))
                continue
            }

            break
        }

        if ($names.Count -gt 0) {
            return @($names.ToArray())
        }
    }

    throw "Model comparison cannot start: class names were not found in the dataset settings."
}

function Read-DataYamlClassInfo([string]$Path) {
    return [pscustomobject]@{
        count = Read-DataYamlClassCount $Path
        names = @(Read-DataYamlClassNames $Path)
    }
}

function Read-WeightsClassInfo([string]$WeightsPath, [string]$Name) {
    $code = @'
import sys
import json
import torch

weights_path = sys.argv[1]
checkpoint = torch.load(weights_path, map_location="cpu")
model = (checkpoint.get("ema") or checkpoint.get("model")) if isinstance(checkpoint, dict) else checkpoint
names = getattr(model, "names", None)
class_count = getattr(model, "nc", None)

def normalize_names(value):
    if isinstance(value, dict):
        def sort_key(key):
            try:
                return int(key)
            except Exception:
                return key
        return [str(value[key]) for key in sorted(value.keys(), key=sort_key)]
    if isinstance(value, (list, tuple)):
        return [str(item) for item in value]
    return None

class_names = normalize_names(names)
if class_count is None and class_names is not None:
    class_count = len(class_names)
if class_count is None:
    raise RuntimeError("class count not found")
if class_names is None:
    raise RuntimeError("class names not found")
print(json.dumps({"count": int(class_count), "names": class_names}, ensure_ascii=False, separators=(",", ":")))
'@

    $previous = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousPythonPath = $env:PYTHONPATH
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $YoloSourceRoot
    } else {
        "$YoloSourceRoot;$previousPythonPath"
    }
    Push-Location $YoloSourceRoot
    try {
        $ErrorActionPreference = "Continue"
        $output = $code | & $PythonExe - $WeightsPath 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "Model comparison cannot read $Name label list: $($output -join ' ')"
        }

        $line = $output | Where-Object { $_ -match '^\s*\{' } | Select-Object -Last 1
        if ($null -eq $line) {
            throw "Model comparison cannot read $Name label list: $($output -join ' ')"
        }

        $data = $line.ToString().Trim() | ConvertFrom-Json
        return [pscustomobject]@{
            count = [int]$data.count
            names = @($data.names | ForEach-Object { $_.ToString() })
        }
    }
    finally {
        Pop-Location
        $ErrorActionPreference = $previousErrorActionPreference
        $env:PYTHONPATH = $previousPythonPath
        $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = $previous
    }
}

function Read-WeightsClassCount([string]$WeightsPath, [string]$Name) {
    return (Read-WeightsClassInfo $WeightsPath $Name).count
}

function Format-ClassInfo($Info) {
    $names = @($Info.names)
    return "$($Info.count) [" + ($names -join ", ") + "]"
}

function Test-ClassNamesEqual($Expected, $Actual) {
    $expectedNames = @($Expected)
    $actualNames = @($Actual)
    if ($expectedNames.Count -ne $actualNames.Count) {
        return $false
    }

    for ($index = 0; $index -lt $expectedNames.Count; $index++) {
        if (-not [string]::Equals($expectedNames[$index], $actualNames[$index], [System.StringComparison]::Ordinal)) {
            return $false
        }
    }

    return $true
}

function Assert-ModelClassCountsMatchData() {
    # Keep this preflight ahead of YOLO val.py so the app can show a clear
    # operator action instead of freezing and then failing deep in Python.
    $dataInfo = Read-DataYamlClassInfo $DataYaml
    $baselineInfo = Read-WeightsClassInfo $BaselineWeights "baseline model"
    $candidateInfo = Read-WeightsClassInfo $CandidateWeights "candidate model"

    $labelsMatch = $baselineInfo.count -eq $dataInfo.count `
        -and $candidateInfo.count -eq $dataInfo.count `
        -and (Test-ClassNamesEqual $dataInfo.names $baselineInfo.names) `
        -and (Test-ClassNamesEqual $dataInfo.names $candidateInfo.names)

    if (-not $labelsMatch) {
        throw "Model comparison cannot start: dataset labels=$(Format-ClassInfo $dataInfo), baseline labels=$(Format-ClassInfo $baselineInfo), candidate labels=$(Format-ClassInfo $candidateInfo). Use models and verification data trained with the same label list."
    }
}

function Find-LatestBestWeights([string]$ProjectRoot) {
    $weights = @("train", "segment") |
        ForEach-Object { Join-Path $ProjectRoot "runs\$_" } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Container } |
        ForEach-Object { Get-ChildItem -LiteralPath $_ -Recurse -File -Filter "best.pt" -ErrorAction SilentlyContinue } |
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

function Invoke-UltralyticsVal([string]$RunName, [string]$WeightsPath, [string]$RunOutputRoot) {
    $logPath = Join-Path $RunOutputRoot "$RunName.log"
    $runProject = Join-Path $RunOutputRoot "runs"
    New-Item -ItemType Directory -Force -Path $runProject | Out-Null

    $code = @'
import json
import sys

from ultralytics import YOLO

weights_path = sys.argv[1]
data_yaml = sys.argv[2]
run_project = sys.argv[3]
run_name = sys.argv[4]
image_size = int(sys.argv[5])
batch_size = int(sys.argv[6])
split_name = sys.argv[7]
model_task = sys.argv[8]

model = YOLO(weights_path)
metrics = model.val(
    data=data_yaml,
    imgsz=image_size,
    batch=batch_size,
    split=split_name,
    project=run_project,
    name=run_name,
    exist_ok=True,
    save_txt=True,
    save_conf=True,
    workers=0,
    task=model_task,
    verbose=False,
)

metric_source = getattr(metrics, "seg", None) if model_task == "segment" else None
if metric_source is None:
    metric_source = getattr(metrics, "box", None)

def scalar(source, names):
    if source is None:
        return None
    for name in names:
        value = getattr(source, name, None)
        if value is None:
            continue
        try:
            return float(value)
        except Exception:
            continue
    return None

payload = {
    "precision": scalar(metric_source, ("mp", "p")),
    "recall": scalar(metric_source, ("mr", "r")),
    "map50": scalar(metric_source, ("map50",)),
    "map5095": scalar(metric_source, ("map", "map5095")),
}
print("OPENVISIONLAB_METRICS_JSON=" + json.dumps(payload, separators=(",", ":")))
'@

    $arguments = @(
        "-",
        $WeightsPath,
        $DataYaml,
        $runProject,
        $RunName,
        $ImageSize.ToString(),
        $BatchSize.ToString(),
        $Task,
        $ModelTask
    )

    $previousPythonPath = $env:PYTHONPATH
    $previousNoWeightsOnly = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $YoloSourceRoot
    } else {
        "$YoloSourceRoot;$previousPythonPath"
    }

    Push-Location $YoloSourceRoot
    try {
        $ErrorActionPreference = "Continue"
        $output = $code | & $PythonExe @arguments 2>&1
        $exitCode = $LASTEXITCODE
        $output | Set-Content -LiteralPath $logPath -Encoding UTF8
        if ($exitCode -ne 0) {
            throw "Ultralytics validation failed for $RunName. See $logPath"
        }
    }
    finally {
        Pop-Location
        $ErrorActionPreference = $previousErrorActionPreference
        $env:PYTHONPATH = $previousPythonPath
        $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = $previousNoWeightsOnly
    }

    return [ordered]@{
        name = $RunName
        weights = $WeightsPath
        logPath = $logPath
        labelsPath = Join-Path $runProject "$RunName\labels"
        metrics = Read-UltralyticsMetrics $logPath
        confidence = Read-PredictionConfidenceSummary (Join-Path $runProject "$RunName\labels") $UiConfidence
    }
}

function Invoke-ModelVal([string]$RunName, [string]$WeightsPath, [string]$RunOutputRoot) {
    if (Test-YoloV5SourceRoot) {
        return Invoke-YoloVal $RunName $WeightsPath $RunOutputRoot
    }

    return Invoke-UltralyticsVal $RunName $WeightsPath $RunOutputRoot
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

function Read-UltralyticsMetrics([string]$LogPath) {
    $line = Get-Content -LiteralPath $LogPath |
        Where-Object { $_ -like "OPENVISIONLAB_METRICS_JSON=*" } |
        Select-Object -Last 1
    if ($null -eq $line) {
        return [ordered]@{ precision = $null; recall = $null; map50 = $null; map5095 = $null }
    }

    try {
        $json = $line.ToString().Substring("OPENVISIONLAB_METRICS_JSON=".Length)
        $data = $json | ConvertFrom-Json
        return [ordered]@{
            precision = $data.precision
            recall = $data.recall
            map50 = $data.map50
            map5095 = $data.map5095
        }
    }
    catch {
        return [ordered]@{ precision = $null; recall = $null; map50 = $null; map5095 = $null }
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

                $confidence = [double]::Parse($parts[$parts.Length - 1], [System.Globalization.CultureInfo]::InvariantCulture)
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

function Get-ModelMetric($Model, [string]$MetricName) {
    if ($null -eq $Model -or $null -eq $Model.metrics) {
        return $null
    }

    $value = $Model.metrics[$MetricName]
    if ($null -eq $value) {
        $value = $Model.metrics.$MetricName
    }

    if ($null -eq $value) {
        return $null
    }

    return [double]$value
}

function New-PromotionRecommendation($Baseline, $Candidate, $Evidence) {
    $minimumPrecision = 0.10
    $heldoutLabelCount = if ($null -eq $Evidence) { 0 } else { [int]$Evidence.comparisonLabelCount }
    $minimumHeldoutLabelCount = if ($null -eq $Evidence) { $RecommendedPromotionLabelCount } else { [int]$Evidence.recommendedLabelCount }
    $baselinePrecision = Get-ModelMetric $Baseline "precision"
    $candidatePrecision = Get-ModelMetric $Candidate "precision"
    $baselineRecall = Get-ModelMetric $Baseline "recall"
    $candidateRecall = Get-ModelMetric $Candidate "recall"
    $baselineMap50 = Get-ModelMetric $Baseline "map50"
    $candidateMap50 = Get-ModelMetric $Candidate "map50"
    $baselineMap5095 = Get-ModelMetric $Baseline "map5095"
    $candidateMap5095 = Get-ModelMetric $Candidate "map5095"

    if ($heldoutLabelCount -lt $minimumHeldoutLabelCount) {
        return [ordered]@{
            recommendation = "hold"
            reason = "Held-out comparison uses $heldoutLabelCount labeled images; collect at least $minimumHeldoutLabelCount before promotion."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    if ($null -eq $candidatePrecision -or $null -eq $candidateRecall -or $null -eq $candidateMap50 -or $null -eq $candidateMap5095) {
        return [ordered]@{
            recommendation = "hold"
            reason = "Candidate metrics are incomplete; keep the current inspection model."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    if ($candidatePrecision -lt $minimumPrecision) {
        return [ordered]@{
            recommendation = "hold"
            reason = "Candidate precision $(Format-NullableNumber $candidatePrecision) is below the minimum $(Format-NullableNumber $minimumPrecision); review labels/training before promotion."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    if ($null -ne $baselineMap50 -and $candidateMap50 -lt $baselineMap50) {
        return [ordered]@{
            recommendation = "hold"
            reason = "Candidate mAP50 is lower than the current inspection model."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    if ($null -ne $baselineMap5095 -and $candidateMap5095 -lt $baselineMap5095) {
        return [ordered]@{
            recommendation = "hold"
            reason = "Candidate mAP50-95 is lower than the current inspection model."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    $betterMap50 = $null -ne $baselineMap50 -and $candidateMap50 -gt $baselineMap50
    $betterMap5095 = $null -ne $baselineMap5095 -and $candidateMap5095 -gt $baselineMap5095
    $precisionNotWorse = $null -eq $baselinePrecision -or $candidatePrecision -ge $baselinePrecision
    $recallNotWorse = $null -eq $baselineRecall -or $candidateRecall -ge $baselineRecall

    if ($betterMap50 -and $betterMap5095 -and $precisionNotWorse -and $recallNotWorse) {
        return [ordered]@{
            recommendation = "promote"
            reason = "Candidate improves mAP and does not regress precision or recall; review examples before saving it as the inspection model."
            minimumPrecision = $minimumPrecision
            heldoutLabelCount = $heldoutLabelCount
            minimumHeldoutLabelCount = $minimumHeldoutLabelCount
        }
    }

    return [ordered]@{
        recommendation = "review"
        reason = "Candidate metrics are mixed; inspect comparison examples before deciding."
        minimumPrecision = $minimumPrecision
        heldoutLabelCount = $heldoutLabelCount
        minimumHeldoutLabelCount = $minimumHeldoutLabelCount
    }
}

function Write-MarkdownReport($Summary, [string]$Path) {
    $baseline = $Summary.baseline
    $candidate = $Summary.candidate
    $promotion = $Summary.promotion
    $evidence = $Summary.evidence
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("# YOLO Model Comparison")
    $lines.Add("")
    $lines.Add("- Data: ``$($Summary.dataYaml)``")
    $lines.Add("- Task: ``$($Summary.task)``")
    $lines.Add("- Model task: ``$($Summary.modelTask)``")
    $lines.Add("- UI confidence: ``$($Summary.uiConfidence)``")
    if ($null -ne $evidence) {
        $lines.Add("- Held-out evidence: ``$($evidence.comparisonLabelCount)`` labeled images / recommended ``$($evidence.recommendedLabelCount)``")
    }
    $lines.Add("")
    $lines.Add("| Model | Precision | Recall | mAP50 | mAP50-95 | UI candidates | Max confidence |")
    $lines.Add("| --- | ---: | ---: | ---: | ---: | ---: | ---: |")
    foreach ($item in @($baseline, $candidate)) {
        $metrics = $item.metrics
        $confidence = $item.confidence
        $lines.Add("| $($item.name) | $(Format-NullableNumber $metrics.precision) | $(Format-NullableNumber $metrics.recall) | $(Format-NullableNumber $metrics.map50) | $(Format-NullableNumber $metrics.map5095) | $($confidence.uiCandidateCount)/$($confidence.predictionCount) | $(Format-NullableNumber $confidence.maxConfidence) |")
    }
    $lines.Add("")
    if ($null -ne $promotion) {
        $lines.Add("## Recommendation")
        $lines.Add("")
        $lines.Add("- Decision: ``$($promotion.recommendation)``")
        $lines.Add("- Reason: $($promotion.reason)")
        $lines.Add("- Minimum precision: ``$(Format-NullableNumber $promotion.minimumPrecision)``")
        $lines.Add("- Held-out labels: ``$($promotion.heldoutLabelCount)`` / required ``$($promotion.minimumHeldoutLabelCount)``")
        $lines.Add("")
    }

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
Assert-YoloValidationRuntime
Assert-File $DataYaml "YOLO data.yaml"
Assert-File $BaselineWeights "Baseline weights"
Assert-File $CandidateWeights "Candidate weights"
Assert-ModelClassCountsMatchData

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runOutputRoot = Join-Path $OutputDirectory $stamp
New-Item -ItemType Directory -Force -Path $runOutputRoot | Out-Null

$baseline = Invoke-ModelVal "baseline" $BaselineWeights $runOutputRoot
$candidate = Invoke-ModelVal "candidate" $CandidateWeights $runOutputRoot
$evidence = New-ComparisonEvidence $DataYaml $Task $ModelTask
$promotion = New-PromotionRecommendation $baseline $candidate $evidence

$summary = [ordered]@{
    createdAt = (Get-Date).ToString("o")
    dataYaml = $DataYaml
    task = $Task
    modelTask = $ModelTask
    uiConfidence = $UiConfidence
    evidence = $evidence
    baseline = $baseline
    candidate = $candidate
    promotion = $promotion
}

$jsonPath = Join-Path $runOutputRoot "comparison-summary.json"
$markdownPath = Join-Path $runOutputRoot "comparison-report.md"
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding UTF8
Write-MarkdownReport $summary $markdownPath

Write-Host "[OK] YOLO comparison complete"
Write-Host "[OK] Summary: $jsonPath"
Write-Host "[OK] Report: $markdownPath"
