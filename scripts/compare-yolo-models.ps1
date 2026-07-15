param(
    [string]$PythonExe = "",
    [string]$YoloProjectRoot = "",
    [string]$YoloSourceRoot = "",
    [string]$DataYaml = "",
    [string]$BaselineWeights = "",
    [string]$CandidateWeights = "",
    [string]$BaselinePythonExe = "",
    [string]$BaselineYoloSourceRoot = "",
    [string]$BaselineEngine = "",
    [string]$CandidatePythonExe = "",
    [string]$CandidateYoloSourceRoot = "",
    [string]$CandidateEngine = "",
    [int]$ImageSize = 320,
    [int]$BatchSize = 16,
    [ValidateRange(1, 10)]
    [int]$BenchmarkRepeatCount = 1,
    [ValidateSet("val", "test")]
    [string]$Task = "val",
    [ValidateSet("detect", "segment")]
    [string]$ModelTask = "detect",
    [string]$SegmentationPositiveClassName = "",
    [double]$UiConfidence = 0.25,
    [string]$OutputDirectory = "artifacts\yolo-model-comparison"
)

$ErrorActionPreference = "Stop"

$RecommendedPromotionLabelCount = 10
$RecommendedSegmentationPositiveLabelCount = 5
$RecommendedSegmentationPositiveImageCount = 5
$RecommendedSegmentationBackgroundImageCount = 5
$MinimumSegmentationUiPositiveImageCoverage = 0.50
$MaximumSegmentationUiBackgroundCandidateRate = 0.10
$script:SegmentationPositiveClassId = $null

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

function Test-YoloV5SourceRoot([string]$SourceRoot) {
    return Test-Path -LiteralPath (Join-Path $SourceRoot "val.py") -PathType Leaf
}

function Test-UltralyticsSourceRoot([string]$SourceRoot) {
    return Test-Path -LiteralPath (Join-Path $SourceRoot "ultralytics") -PathType Container
}

function Assert-YoloValidationRuntime([string]$SourceRoot, [string]$Name) {
    if ((Test-YoloV5SourceRoot $SourceRoot) -or (Test-UltralyticsSourceRoot $SourceRoot)) {
        return
    }

    throw "$Name validation runtime not found: expected val.py or ultralytics package under $SourceRoot"
}

function Resolve-EngineName([string]$Engine, [string]$SourceRoot) {
    if (-not [string]::IsNullOrWhiteSpace($Engine)) {
        return $Engine.Trim()
    }

    if (Test-YoloV5SourceRoot $SourceRoot) {
        return "YOLOv5"
    }

    return "YOLOv8"
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

function Resolve-DataYamlSplitPath([string]$DataYamlPath, [string]$SplitName) {
    $values = Read-DataYamlScalarValues $DataYamlPath
    $splitPath = if ($values.ContainsKey($SplitName)) { $values[$SplitName] } else { "" }
    $yamlRootPath = if ($values.ContainsKey("path")) { $values["path"] } else { "" }
    if ([string]::IsNullOrWhiteSpace($splitPath)) {
        return ""
    }

    return Resolve-DataYamlValuePath $DataYamlPath $yamlRootPath $splitPath
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

function Test-YoloLabelLineClass([string]$Line, $ClassId) {
    if ($null -eq $ClassId) {
        return $true
    }

    $tokens = @($Line -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($tokens.Count -eq 0) {
        return $false
    }

    $lineClassId = 0
    return [int]::TryParse($tokens[0], [System.Globalization.NumberStyles]::Integer, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$lineClassId) `
        -and $lineClassId -eq [int]$ClassId
}

function Test-SegmentationLabelLineForClass([string]$Line, $ClassId) {
    return (Test-SegmentationLabelLine $Line) -and (Test-YoloLabelLineClass $Line $ClassId)
}

function New-ComparisonEvidence([string]$DataYamlPath, [string]$SplitName, [string]$ValidationTask, $PositiveClass) {
    $resolvedSplitPath = Resolve-DataYamlSplitPath $DataYamlPath $SplitName
    $imagePaths = @(Get-SplitImagePaths $resolvedSplitPath)
    $labelPaths = @($imagePaths |
        ForEach-Object { Resolve-LabelPathFromImagePath $_ } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
    $positiveClassId = if ($null -eq $PositiveClass) { $null } else { [int]$PositiveClass.id }
    $positiveSegmentationLabelLineCount = if ($ValidationTask -ieq "segment") {
        @($labelPaths |
            ForEach-Object { Get-Content -LiteralPath $_ } |
            ForEach-Object { (Remove-YamlInlineComment $_).Trim() } |
            Where-Object { Test-SegmentationLabelLineForClass $_ $positiveClassId }).Count
    } else {
        $null
    }
    $positiveSegmentationImageCount = if ($ValidationTask -ieq "segment") {
        @($labelPaths |
            Where-Object {
                $labelPath = $_
                @(Get-Content -LiteralPath $labelPath |
                    ForEach-Object { (Remove-YamlInlineComment $_).Trim() } |
                    Where-Object { Test-SegmentationLabelLineForClass $_ $positiveClassId }).Count -gt 0
            }).Count
    } else {
        $null
    }
    $backgroundSegmentationImageCount = if ($ValidationTask -ieq "segment") {
        $labelPaths.Count - $positiveSegmentationImageCount
    } else {
        $null
    }

    $result = [ordered]@{
        split = $SplitName
        imagesPath = $resolvedSplitPath
        imageCount = $imagePaths.Count
        labelFileCount = $labelPaths.Count
        comparisonLabelCount = [System.Math]::Min($imagePaths.Count, $labelPaths.Count)
        positiveSegmentationLabelLineCount = $positiveSegmentationLabelLineCount
        positiveSegmentationImageCount = $positiveSegmentationImageCount
        backgroundSegmentationImageCount = $backgroundSegmentationImageCount
        recommendedLabelCount = $RecommendedPromotionLabelCount
    }

    if ($null -ne $PositiveClass) {
        $result["segmentationPositiveClassId"] = [int]$PositiveClass.id
        $result["segmentationPositiveClassName"] = $PositiveClass.name
    }

    return $result
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

function Resolve-SegmentationPositiveClass($DataInfo, [string]$ClassName, [string]$ValidationTask) {
    if ([string]::IsNullOrWhiteSpace($ClassName)) {
        return $null
    }

    if ($ValidationTask -ine "segment") {
        throw "Segmentation positive class can only be used with ModelTask=segment."
    }

    $names = @($DataInfo.names)
    for ($index = 0; $index -lt $names.Count; $index++) {
        if ([string]::Equals($names[$index], $ClassName.Trim(), [System.StringComparison]::OrdinalIgnoreCase)) {
            return [pscustomobject]@{
                id = $index
                name = $names[$index]
            }
        }
    }

    throw "Model comparison cannot start: segmentation positive class '$ClassName' was not found in dataset labels=$(Format-ClassInfo $DataInfo)."
}

function Read-WeightsClassInfo(
    [string]$WeightsPath,
    [string]$Name,
    [string]$RuntimePythonExe = $PythonExe,
    [string]$RuntimeSourceRoot = $YoloSourceRoot
) {
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

task = getattr(model, "task", None)
if task is None:
    model_type = model.__class__.__name__.lower()
    if "segment" in model_type:
        task = "segment"
    else:
        try:
            head_type = model.model[-1].__class__.__name__.lower()
            task = "segment" if "segment" in head_type else "detect"
        except Exception:
            task = "detect"

print(json.dumps({"count": int(class_count), "names": class_names, "task": str(task).lower()}, ensure_ascii=False, separators=(",", ":")))
'@

    $previous = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousPythonPath = $env:PYTHONPATH
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $RuntimeSourceRoot
    } else {
        "$RuntimeSourceRoot;$previousPythonPath"
    }
    Push-Location $RuntimeSourceRoot
    try {
        $ErrorActionPreference = "Continue"
        $output = $code | & $RuntimePythonExe - $WeightsPath 2>&1
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
            task = $data.task.ToString()
        }
    }
    finally {
        Pop-Location
        $ErrorActionPreference = $previousErrorActionPreference
        $env:PYTHONPATH = $previousPythonPath
        $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = $previous
    }
}

function Read-WeightsClassCount(
    [string]$WeightsPath,
    [string]$Name,
    [string]$RuntimePythonExe = $PythonExe,
    [string]$RuntimeSourceRoot = $YoloSourceRoot
) {
    return (Read-WeightsClassInfo $WeightsPath $Name $RuntimePythonExe $RuntimeSourceRoot).count
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
    $baselineInfo = Read-WeightsClassInfo $BaselineWeights "baseline model" $BaselinePythonExe $BaselineYoloSourceRoot
    $candidateInfo = Read-WeightsClassInfo $CandidateWeights "candidate model" $CandidatePythonExe $CandidateYoloSourceRoot

    $labelsMatch = $baselineInfo.count -eq $dataInfo.count `
        -and $candidateInfo.count -eq $dataInfo.count `
        -and (Test-ClassNamesEqual $dataInfo.names $baselineInfo.names) `
        -and (Test-ClassNamesEqual $dataInfo.names $candidateInfo.names)

    if (-not $labelsMatch) {
        throw "Model comparison cannot start: dataset labels=$(Format-ClassInfo $dataInfo), baseline labels=$(Format-ClassInfo $baselineInfo), candidate labels=$(Format-ClassInfo $candidateInfo). Use models and verification data trained with the same label list."
    }

    if ($baselineInfo.task -ine $ModelTask -or $candidateInfo.task -ine $ModelTask) {
        throw "Model comparison cannot start: requested task=$ModelTask, baseline task=$($baselineInfo.task), candidate task=$($candidateInfo.task). Compare models trained for the same task."
    }

    return $dataInfo
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

function Invoke-YoloVal(
    [string]$RunName,
    [string]$WeightsPath,
    [string]$RunOutputRoot,
    [string]$RuntimePythonExe,
    [string]$RuntimeSourceRoot,
    [string]$Engine,
    [bool]$IncludePredictions = $true
) {
    $logPath = Join-Path $RunOutputRoot "$RunName.log"
    $runProject = Join-Path $RunOutputRoot "runs"
    New-Item -ItemType Directory -Force -Path $runProject | Out-Null

    $arguments = @(
        (Join-Path $RuntimeSourceRoot "val.py"),
        "--weights", $WeightsPath,
        "--data", $DataYaml,
        "--img", $ImageSize.ToString(),
        "--batch-size", $BatchSize.ToString(),
        "--task", $Task,
        "--workers", "0",
        "--project", $runProject,
        "--name", $RunName,
        "--exist-ok"
    )
    if ($IncludePredictions) {
        $arguments += @("--save-txt", "--save-conf")
    }

    $previous = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    try {
        $ErrorActionPreference = "Continue"
        $output = & $RuntimePythonExe @arguments 2>&1
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

    $labelsPath = Join-Path $runProject "$RunName\labels"
    return [ordered]@{
        name = $RunName
        engine = $Engine
        weights = $WeightsPath
        logPath = $logPath
        labelsPath = $labelsPath
        metrics = Read-ValMetrics $logPath
        benchmark = Read-ValBenchmark $logPath
        confidence = if ($IncludePredictions) { Read-PredictionConfidenceSummary $labelsPath $UiConfidence $script:SegmentationPositiveClassId } else { $null }
    }
}

function Invoke-UltralyticsVal(
    [string]$RunName,
    [string]$WeightsPath,
    [string]$RunOutputRoot,
    [string]$RuntimePythonExe,
    [string]$RuntimeSourceRoot,
    [string]$Engine,
    [bool]$IncludePredictions = $true
) {
    $logPath = Join-Path $RunOutputRoot "$RunName.log"
    $runProject = Join-Path $RunOutputRoot "runs"
    $predictSource = Resolve-DataYamlSplitPath $DataYaml $Task
    if ([string]::IsNullOrWhiteSpace($predictSource) -or -not (Test-Path -LiteralPath $predictSource)) {
        throw "YOLO prediction source not found for $Task split: $predictSource"
    }

    New-Item -ItemType Directory -Force -Path $runProject | Out-Null

    $code = @'
import json
import sys
from pathlib import Path

from ultralytics import YOLO

weights_path = sys.argv[1]
data_yaml = sys.argv[2]
run_project = sys.argv[3]
run_name = sys.argv[4]
image_size = int(sys.argv[5])
batch_size = int(sys.argv[6])
split_name = sys.argv[7]
model_task = sys.argv[8]
predict_source = sys.argv[9]
include_predictions = sys.argv[10].lower() == "true"

model = YOLO(weights_path)
metrics = model.val(
    data=data_yaml,
    imgsz=image_size,
    batch=batch_size,
    split=split_name,
    project=run_project,
    name=run_name,
    exist_ok=True,
    save_txt=include_predictions,
    save_conf=include_predictions,
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

speed = getattr(metrics, "speed", None) or {}
preprocess_ms = speed.get("preprocess")
inference_ms = speed.get("inference")
postprocess_ms = speed.get("postprocess")
parts = [value for value in (preprocess_ms, inference_ms, postprocess_ms) if value is not None]
benchmark = {
    "preprocessMs": float(preprocess_ms) if preprocess_ms is not None else None,
    "inferenceMs": float(inference_ms) if inference_ms is not None else None,
    "postprocessMs": float(postprocess_ms) if postprocess_ms is not None else None,
    "taktMs": float(sum(parts)) if len(parts) == 3 else None,
    "source": "native-validation-speed",
}
print("OPENVISIONLAB_BENCHMARK_JSON=" + json.dumps(benchmark, separators=(",", ":")))

if include_predictions:
    predict_name = run_name + "-predict"
    model.predict(
        source=predict_source,
        imgsz=image_size,
        conf=0.001,
        iou=0.7,
        project=run_project,
        name=predict_name,
        exist_ok=True,
        save_txt=True,
        save_conf=True,
        verbose=False,
    )
    print("OPENVISIONLAB_PREDICT_LABELS=" + str(Path(run_project) / predict_name / "labels"))
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
        $ModelTask,
        $predictSource,
        $IncludePredictions.ToString().ToLowerInvariant()
    )

    $previousPythonPath = $env:PYTHONPATH
    $previousNoWeightsOnly = $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD
    $previousErrorActionPreference = $ErrorActionPreference
    $env:TORCH_FORCE_NO_WEIGHTS_ONLY_LOAD = "1"
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $RuntimeSourceRoot
    } else {
        "$RuntimeSourceRoot;$previousPythonPath"
    }

    Push-Location $RuntimeSourceRoot
    try {
        $ErrorActionPreference = "Continue"
        $output = $code | & $RuntimePythonExe @arguments 2>&1
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

    $predictLabelsPath = Join-Path $runProject "$RunName-predict\labels"
    return [ordered]@{
        name = $RunName
        engine = $Engine
        weights = $WeightsPath
        logPath = $logPath
        validationLabelsPath = Join-Path $runProject "$RunName\labels"
        labelsPath = $predictLabelsPath
        metrics = Read-UltralyticsMetrics $logPath
        benchmark = Read-UltralyticsBenchmark $logPath
        confidence = if ($IncludePredictions) { Read-PredictionConfidenceSummary $predictLabelsPath $UiConfidence $script:SegmentationPositiveClassId } else { $null }
    }
}

function Invoke-ModelVal(
    [string]$RunName,
    [string]$WeightsPath,
    [string]$RunOutputRoot,
    [string]$RuntimePythonExe,
    [string]$RuntimeSourceRoot,
    [string]$Engine,
    [bool]$IncludePredictions = $true
) {
    if (Test-YoloV5SourceRoot $RuntimeSourceRoot) {
        return Invoke-YoloVal $RunName $WeightsPath $RunOutputRoot $RuntimePythonExe $RuntimeSourceRoot $Engine $IncludePredictions
    }

    return Invoke-UltralyticsVal $RunName $WeightsPath $RunOutputRoot $RuntimePythonExe $RuntimeSourceRoot $Engine $IncludePredictions
}

function Get-Median($Values) {
    $numbers = @($Values | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ } | Sort-Object)
    if ($numbers.Count -eq 0) {
        return $null
    }

    $middle = [int][Math]::Floor($numbers.Count / 2)
    if (($numbers.Count % 2) -eq 1) {
        return $numbers[$middle]
    }

    return ($numbers[$middle - 1] + $numbers[$middle]) / 2
}

function New-AggregatedBenchmark($Benchmarks, [int]$RequestedRepeatCount) {
    $valid = @($Benchmarks | Where-Object { $null -ne $_ -and $null -ne $_.taktMs })
    if ($RequestedRepeatCount -gt 1 -and $valid.Count -ne $RequestedRepeatCount) {
        throw "Model comparison benchmark failed: requested $RequestedRepeatCount timing samples but collected $($valid.Count)."
    }

    $taktSamples = @($valid | ForEach-Object { [double]$_.taktMs })
    return [ordered]@{
        preprocessMs = Get-Median @($valid | ForEach-Object { $_.preprocessMs })
        inferenceMs = Get-Median @($valid | ForEach-Object { $_.inferenceMs })
        postprocessMs = Get-Median @($valid | ForEach-Object { $_.postprocessMs })
        taktMs = Get-Median $taktSamples
        taktMinMs = if ($taktSamples.Count -gt 0) { ($taktSamples | Measure-Object -Minimum).Minimum } else { $null }
        taktMaxMs = if ($taktSamples.Count -gt 0) { ($taktSamples | Measure-Object -Maximum).Maximum } else { $null }
        repeatCount = $valid.Count
        requestedRepeatCount = $RequestedRepeatCount
        taktSamplesMs = $taktSamples
        source = if ($RequestedRepeatCount -gt 1) { "native-validation-speed-median" } else { "native-validation-speed" }
    }
}

function Invoke-ModelValWithBenchmarkRepeats(
    [string]$RunName,
    [string]$WeightsPath,
    [string]$RunOutputRoot,
    [string]$RuntimePythonExe,
    [string]$RuntimeSourceRoot,
    [string]$Engine,
    [int]$RepeatCount
) {
    $result = Invoke-ModelVal $RunName $WeightsPath $RunOutputRoot $RuntimePythonExe $RuntimeSourceRoot $Engine $true
    $benchmarks = @($result.benchmark)
    $logPaths = @($result.logPath)
    if ($RepeatCount -gt 1) {
        for ($repeat = 2; $repeat -le $RepeatCount; $repeat++) {
            $repeatResult = Invoke-ModelVal "$RunName-benchmark-$repeat" $WeightsPath $RunOutputRoot $RuntimePythonExe $RuntimeSourceRoot $Engine $false
            $benchmarks += $repeatResult.benchmark
            $logPaths += $repeatResult.logPath
        }
    }

    $result["benchmark"] = New-AggregatedBenchmark $benchmarks $RepeatCount
    $result["benchmarkLogPaths"] = $logPaths
    return $result
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

function Read-ValBenchmark([string]$LogPath) {
    $text = Get-Content -LiteralPath $LogPath -Raw
    $pattern = '(?im)Speed:\s*([0-9.]+)ms\s+pre-process,\s*([0-9.]+)ms\s+inference,\s*([0-9.]+)ms\s+(?:NMS|postprocess)\s+per image'
    $match = [regex]::Match($text, $pattern)
    if (-not $match.Success) {
        return [ordered]@{ preprocessMs = $null; inferenceMs = $null; postprocessMs = $null; taktMs = $null; source = "native-validation-speed" }
    }

    $preprocessMs = [double]$match.Groups[1].Value
    $inferenceMs = [double]$match.Groups[2].Value
    $postprocessMs = [double]$match.Groups[3].Value
    return [ordered]@{
        preprocessMs = $preprocessMs
        inferenceMs = $inferenceMs
        postprocessMs = $postprocessMs
        taktMs = $preprocessMs + $inferenceMs + $postprocessMs
        source = "native-validation-speed"
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

function Read-UltralyticsBenchmark([string]$LogPath) {
    $line = Get-Content -LiteralPath $LogPath |
        Where-Object { $_ -like "OPENVISIONLAB_BENCHMARK_JSON=*" } |
        Select-Object -Last 1
    if ($null -eq $line) {
        return [ordered]@{ preprocessMs = $null; inferenceMs = $null; postprocessMs = $null; taktMs = $null; source = "native-validation-speed" }
    }

    try {
        $json = $line.ToString().Substring("OPENVISIONLAB_BENCHMARK_JSON=".Length)
        $data = $json | ConvertFrom-Json
        return [ordered]@{
            preprocessMs = $data.preprocessMs
            inferenceMs = $data.inferenceMs
            postprocessMs = $data.postprocessMs
            taktMs = $data.taktMs
            source = $data.source
        }
    }
    catch {
        return [ordered]@{ preprocessMs = $null; inferenceMs = $null; postprocessMs = $null; taktMs = $null; source = "native-validation-speed" }
    }
}

function Read-PredictionConfidenceSummary([string]$LabelsPath, [double]$Threshold, $PositiveClassId) {
    $total = 0
    $aboveThreshold = 0
    $maxConfidence = 0.0
    if (Test-Path -LiteralPath $LabelsPath -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $LabelsPath -File -Filter "*.txt") {
            foreach ($line in Get-Content -LiteralPath $file.FullName) {
                $parts = $line -split '\s+'
                if ($parts.Length -lt 6 -or -not (Test-YoloLabelLineClass $line $PositiveClassId)) {
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
        thresholdSweep = @(Read-PredictionThresholdSweep $LabelsPath $Threshold $PositiveClassId)
    }
}

function Test-PredictionFileAtThreshold([string]$Path, [double]$Threshold, $PositiveClassId) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        $parts = @($line -split '\s+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($parts.Count -lt 6 -or -not (Test-YoloLabelLineClass $line $PositiveClassId)) {
            continue
        }

        $confidence = 0.0
        if ([double]::TryParse($parts[$parts.Count - 1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$confidence) -and $confidence -ge $Threshold) {
            return $true
        }
    }

    return $false
}

function Read-SegmentationPredictionImageEvidence($Model, $Evidence, [double]$Threshold) {
    $positiveImageCount = 0
    $positiveImagesWithCandidates = 0
    $backgroundImageCount = 0
    $backgroundImagesWithCandidates = 0
    $positiveClassId = if ($Evidence.Contains("segmentationPositiveClassId")) { [int]$Evidence.segmentationPositiveClassId } else { $null }

    foreach ($imagePath in @(Get-SplitImagePaths $Evidence.imagesPath)) {
        $answerLabelPath = Resolve-LabelPathFromImagePath $imagePath
        if (-not (Test-Path -LiteralPath $answerLabelPath -PathType Leaf)) {
            continue
        }

        $isPositive = @(Get-Content -LiteralPath $answerLabelPath |
            ForEach-Object { (Remove-YamlInlineComment $_).Trim() } |
            Where-Object { Test-SegmentationLabelLineForClass $_ $positiveClassId }).Count -gt 0
        $predictionLabelPath = Join-Path $Model.labelsPath (([System.IO.Path]::GetFileNameWithoutExtension($imagePath)) + ".txt")
        $hasUiCandidate = Test-PredictionFileAtThreshold $predictionLabelPath $Threshold $positiveClassId
        if ($isPositive) {
            $positiveImageCount++
            if ($hasUiCandidate) {
                $positiveImagesWithCandidates++
            }
        }
        else {
            $backgroundImageCount++
            if ($hasUiCandidate) {
                $backgroundImagesWithCandidates++
            }
        }
    }

    return [ordered]@{
        uiPositiveImageCount = $positiveImageCount
        uiPositiveImagesWithCandidates = $positiveImagesWithCandidates
        uiPositiveImageCoverage = if ($positiveImageCount -gt 0) { [double]$positiveImagesWithCandidates / $positiveImageCount } else { $null }
        uiBackgroundImageCount = $backgroundImageCount
        uiBackgroundImagesWithCandidates = $backgroundImagesWithCandidates
        uiBackgroundCandidateRate = if ($backgroundImageCount -gt 0) { [double]$backgroundImagesWithCandidates / $backgroundImageCount } else { $null }
    }
}

function Add-SegmentationPredictionImageEvidence($Model, $Evidence, [double]$Threshold) {
    if ($null -eq $Model -or $null -eq $Model.confidence -or $null -eq $Evidence) {
        return
    }

    $imageEvidence = Read-SegmentationPredictionImageEvidence $Model $Evidence $Threshold
    foreach ($key in $imageEvidence.Keys) {
        $Model.confidence[$key] = $imageEvidence[$key]
    }
}

function Get-ReviewConfidenceThresholds([double]$UiConfidence) {
    $thresholds = New-Object System.Collections.Generic.List[double]
    foreach ($threshold in @($UiConfidence, 0.25, 0.10, 0.05, 0.01)) {
        if (-not ($thresholds | Where-Object { [Math]::Abs($_ - $threshold) -lt 0.000001 })) {
            $thresholds.Add([double]$threshold)
        }
    }

    return @($thresholds | Sort-Object -Descending)
}

function Read-PredictionThresholdSweep([string]$LabelsPath, [double]$UiConfidence, $PositiveClassId) {
    $thresholds = Get-ReviewConfidenceThresholds $UiConfidence
    $counts = @{}
    foreach ($threshold in $thresholds) {
        $counts[$threshold.ToString("0.######", [System.Globalization.CultureInfo]::InvariantCulture)] = 0
    }

    if (Test-Path -LiteralPath $LabelsPath -PathType Container) {
        foreach ($file in Get-ChildItem -LiteralPath $LabelsPath -File -Filter "*.txt") {
            foreach ($line in Get-Content -LiteralPath $file.FullName) {
                $parts = $line -split '\s+'
                if ($parts.Length -lt 6 -or -not (Test-YoloLabelLineClass $line $PositiveClassId)) {
                    continue
                }

                $confidence = [double]::Parse($parts[$parts.Length - 1], [System.Globalization.CultureInfo]::InvariantCulture)
                foreach ($threshold in $thresholds) {
                    if ($confidence -ge $threshold) {
                        $key = $threshold.ToString("0.######", [System.Globalization.CultureInfo]::InvariantCulture)
                        $counts[$key] = [int]$counts[$key] + 1
                    }
                }
            }
        }
    }

    foreach ($threshold in $thresholds) {
        $key = $threshold.ToString("0.######", [System.Globalization.CultureInfo]::InvariantCulture)
        [ordered]@{
            confidence = $threshold
            uiCandidateCount = [int]$counts[$key]
        }
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

function Get-ModelBenchmarkValue($Model, [string]$ValueName) {
    if ($null -eq $Model -or $null -eq $Model.benchmark) {
        return $null
    }

    return $Model.benchmark.$ValueName
}

function Get-ModelConfidenceValue($Model, [string]$ValueName) {
    if ($null -eq $Model -or $null -eq $Model.confidence) {
        return $null
    }

    $value = $Model.confidence[$ValueName]
    if ($null -eq $value) {
        $value = $Model.confidence.$ValueName
    }

    if ($null -eq $value) {
        return $null
    }

    return [double]$value
}

function New-PromotionRecommendationResult(
    [string]$Recommendation,
    [System.Collections.IEnumerable]$Reasons,
    [double]$MinimumPrecision,
    [int]$HeldoutLabelCount,
    [int]$MinimumHeldoutLabelCount,
    [int]$MinimumUiCandidateCount,
    $CandidateUiCandidateCount,
    $PositiveSegmentationLabelLineCount,
    [int]$MinimumPositiveSegmentationLabelLineCount,
    $PositiveSegmentationImageCount,
    [int]$MinimumPositiveSegmentationImageCount,
    $SegmentationOperatingEvidence) {
    $reasonList = @()
    if ($null -ne $Reasons) {
        foreach ($reason in $Reasons) {
            if (-not [string]::IsNullOrWhiteSpace($reason)) {
                $reasonList += $reason
            }
        }
    }

    $primaryReason = if ($reasonList.Count -gt 0) { $reasonList[0] } else { "" }
    $result = [ordered]@{
        recommendation = $Recommendation
        reason = $primaryReason
        reasons = @($reasonList)
        minimumPrecision = $MinimumPrecision
        heldoutLabelCount = $HeldoutLabelCount
        minimumHeldoutLabelCount = $MinimumHeldoutLabelCount
    }

    if ($null -ne $CandidateUiCandidateCount) {
        $result["minimumUiCandidateCount"] = $MinimumUiCandidateCount
        $result["uiCandidateCount"] = [int]$CandidateUiCandidateCount
    }

    if ($null -ne $PositiveSegmentationLabelLineCount) {
        $result["minimumPositiveSegmentationLabelLineCount"] = $MinimumPositiveSegmentationLabelLineCount
        $result["positiveSegmentationLabelLineCount"] = [int]$PositiveSegmentationLabelLineCount
    }

    if ($null -ne $PositiveSegmentationImageCount) {
        $result["minimumPositiveSegmentationImageCount"] = $MinimumPositiveSegmentationImageCount
        $result["positiveSegmentationImageCount"] = [int]$PositiveSegmentationImageCount
    }

    if ($null -ne $SegmentationOperatingEvidence) {
        foreach ($key in $SegmentationOperatingEvidence.Keys) {
            $result[$key] = $SegmentationOperatingEvidence[$key]
        }
    }

    return $result
}

function New-PromotionRecommendation($Baseline, $Candidate, $Evidence, [double]$UiConfidence) {
    $minimumPrecision = 0.10
    $heldoutLabelCount = if ($null -eq $Evidence) { 0 } else { [int]$Evidence.comparisonLabelCount }
    $minimumHeldoutLabelCount = if ($null -eq $Evidence) { $RecommendedPromotionLabelCount } else { [int]$Evidence.recommendedLabelCount }
    $positiveSegmentationLabelLineCount = if ($null -eq $Evidence -or $null -eq $Evidence.positiveSegmentationLabelLineCount) { $null } else { [int]$Evidence.positiveSegmentationLabelLineCount }
    $minimumPositiveSegmentationLabelLineCount = $RecommendedSegmentationPositiveLabelCount
    $positiveSegmentationImageCount = if ($null -eq $Evidence -or $null -eq $Evidence.positiveSegmentationImageCount) { $null } else { [int]$Evidence.positiveSegmentationImageCount }
    $minimumPositiveSegmentationImageCount = $RecommendedSegmentationPositiveImageCount
    $backgroundSegmentationImageCount = if ($null -eq $Evidence -or $null -eq $Evidence.backgroundSegmentationImageCount) { $null } else { [int]$Evidence.backgroundSegmentationImageCount }
    $candidateUiPositiveImageCount = Get-ModelConfidenceValue $Candidate "uiPositiveImageCount"
    $candidateUiPositiveImagesWithCandidates = Get-ModelConfidenceValue $Candidate "uiPositiveImagesWithCandidates"
    $candidateUiPositiveImageCoverage = Get-ModelConfidenceValue $Candidate "uiPositiveImageCoverage"
    $candidateUiBackgroundImageCount = Get-ModelConfidenceValue $Candidate "uiBackgroundImageCount"
    $candidateUiBackgroundImagesWithCandidates = Get-ModelConfidenceValue $Candidate "uiBackgroundImagesWithCandidates"
    $candidateUiBackgroundCandidateRate = Get-ModelConfidenceValue $Candidate "uiBackgroundCandidateRate"
    $segmentationOperatingEvidence = $null
    if ($null -ne $positiveSegmentationImageCount) {
        $segmentationOperatingEvidence = [ordered]@{
            minimumBackgroundSegmentationImageCount = $RecommendedSegmentationBackgroundImageCount
            backgroundSegmentationImageCount = $backgroundSegmentationImageCount
            minimumUiPositiveImageCoverage = $MinimumSegmentationUiPositiveImageCoverage
            uiPositiveImageCount = $candidateUiPositiveImageCount
            uiPositiveImagesWithCandidates = $candidateUiPositiveImagesWithCandidates
            uiPositiveImageCoverage = $candidateUiPositiveImageCoverage
            maximumUiBackgroundCandidateRate = $MaximumSegmentationUiBackgroundCandidateRate
            uiBackgroundImageCount = $candidateUiBackgroundImageCount
            uiBackgroundImagesWithCandidates = $candidateUiBackgroundImagesWithCandidates
            uiBackgroundCandidateRate = $candidateUiBackgroundCandidateRate
        }

        if ($Evidence.Contains("segmentationPositiveClassId")) {
            $segmentationOperatingEvidence["segmentationPositiveClassId"] = [int]$Evidence.segmentationPositiveClassId
            $segmentationOperatingEvidence["segmentationPositiveClassName"] = $Evidence.segmentationPositiveClassName
        }
    }
    $minimumUiCandidateCount = 1
    $baselinePrecision = Get-ModelMetric $Baseline "precision"
    $candidatePrecision = Get-ModelMetric $Candidate "precision"
    $baselineRecall = Get-ModelMetric $Baseline "recall"
    $candidateRecall = Get-ModelMetric $Candidate "recall"
    $baselineMap50 = Get-ModelMetric $Baseline "map50"
    $candidateMap50 = Get-ModelMetric $Candidate "map50"
    $baselineMap5095 = Get-ModelMetric $Baseline "map5095"
    $candidateMap5095 = Get-ModelMetric $Candidate "map5095"
    $candidateUiCandidateCount = Get-ModelConfidenceValue $Candidate "uiCandidateCount"
    $holdReasons = New-Object System.Collections.Generic.List[string]

    if ($heldoutLabelCount -lt $minimumHeldoutLabelCount) {
        $holdReasons.Add("Held-out comparison uses $heldoutLabelCount labeled images; collect at least $minimumHeldoutLabelCount before promotion.")
    }

    if ($null -ne $positiveSegmentationLabelLineCount -and $positiveSegmentationLabelLineCount -lt $minimumPositiveSegmentationLabelLineCount) {
        $holdReasons.Add("Segment held-out comparison uses $positiveSegmentationLabelLineCount positive segmentation labels; collect at least $minimumPositiveSegmentationLabelLineCount positive mask labels before promotion.")
    }

    if ($null -ne $positiveSegmentationImageCount -and $positiveSegmentationImageCount -lt $minimumPositiveSegmentationImageCount) {
        $holdReasons.Add("Segment held-out comparison uses $positiveSegmentationImageCount positive segmentation images; collect at least $minimumPositiveSegmentationImageCount positive mask images before promotion.")
    }

    if ($null -ne $backgroundSegmentationImageCount -and $backgroundSegmentationImageCount -lt $RecommendedSegmentationBackgroundImageCount) {
        $holdReasons.Add("Segment held-out comparison uses $backgroundSegmentationImageCount background segmentation images; collect at least $RecommendedSegmentationBackgroundImageCount background images before promotion.")
    }

    if ($null -ne $positiveSegmentationImageCount -and ($null -eq $candidateUiPositiveImageCoverage -or $null -eq $candidateUiBackgroundCandidateRate)) {
        $holdReasons.Add("Candidate UI-threshold image evidence is incomplete; rerun the held-out comparison before promotion.")
    }
    else {
        if ($null -ne $candidateUiPositiveImageCoverage -and $candidateUiPositiveImageCoverage -lt $MinimumSegmentationUiPositiveImageCoverage) {
            $holdReasons.Add("Candidate UI-threshold positive image coverage $([int]$candidateUiPositiveImagesWithCandidates)/$([int]$candidateUiPositiveImageCount) ($(Format-NullableNumber $candidateUiPositiveImageCoverage)) is below minimum $(Format-NullableNumber $MinimumSegmentationUiPositiveImageCoverage) at confidence $(Format-NullableNumber $UiConfidence); add varied training data or tune the model before promotion.")
        }

        if ($null -ne $candidateUiBackgroundCandidateRate -and $candidateUiBackgroundCandidateRate -gt $MaximumSegmentationUiBackgroundCandidateRate) {
            $holdReasons.Add("Candidate UI-threshold background candidate rate $([int]$candidateUiBackgroundImagesWithCandidates)/$([int]$candidateUiBackgroundImageCount) ($(Format-NullableNumber $candidateUiBackgroundCandidateRate)) exceeds maximum $(Format-NullableNumber $MaximumSegmentationUiBackgroundCandidateRate) at confidence $(Format-NullableNumber $UiConfidence); add background data or tune the model before promotion.")
        }
    }

    if ($null -eq $candidatePrecision -or $null -eq $candidateRecall -or $null -eq $candidateMap50 -or $null -eq $candidateMap5095) {
        $holdReasons.Add("Candidate metrics are incomplete; keep the current inspection model.")
        return New-PromotionRecommendationResult "hold" $holdReasons $minimumPrecision $heldoutLabelCount $minimumHeldoutLabelCount $minimumUiCandidateCount $candidateUiCandidateCount $positiveSegmentationLabelLineCount $minimumPositiveSegmentationLabelLineCount $positiveSegmentationImageCount $minimumPositiveSegmentationImageCount $segmentationOperatingEvidence
    }

    if ($candidatePrecision -lt $minimumPrecision) {
        $holdReasons.Add("Candidate precision $(Format-NullableNumber $candidatePrecision) is below the minimum $(Format-NullableNumber $minimumPrecision); review labels/training before promotion.")
    }

    if ($null -ne $candidateUiCandidateCount -and $candidateUiCandidateCount -lt $minimumUiCandidateCount) {
        $holdReasons.Add("Candidate produced 0 UI-threshold candidates at confidence $(Format-NullableNumber $UiConfidence); lower the review threshold or retrain before promotion.")
    }

    if ($null -ne $baselineMap50 -and $candidateMap50 -lt $baselineMap50) {
        $holdReasons.Add("Candidate mAP50 is lower than the current inspection model.")
    }

    if ($null -ne $baselineMap5095 -and $candidateMap5095 -lt $baselineMap5095) {
        $holdReasons.Add("Candidate mAP50-95 is lower than the current inspection model.")
    }

    if ($holdReasons.Count -gt 0) {
        return New-PromotionRecommendationResult "hold" $holdReasons $minimumPrecision $heldoutLabelCount $minimumHeldoutLabelCount $minimumUiCandidateCount $candidateUiCandidateCount $positiveSegmentationLabelLineCount $minimumPositiveSegmentationLabelLineCount $positiveSegmentationImageCount $minimumPositiveSegmentationImageCount $segmentationOperatingEvidence
    }

    $betterMap50 = $null -ne $baselineMap50 -and $candidateMap50 -gt $baselineMap50
    $betterMap5095 = $null -ne $baselineMap5095 -and $candidateMap5095 -gt $baselineMap5095
    $precisionNotWorse = $null -eq $baselinePrecision -or $candidatePrecision -ge $baselinePrecision
    $recallNotWorse = $null -eq $baselineRecall -or $candidateRecall -ge $baselineRecall

    if ($betterMap50 -and $betterMap5095 -and $precisionNotWorse -and $recallNotWorse) {
        return New-PromotionRecommendationResult "promote" @("Candidate improves mAP and does not regress precision or recall; review examples before saving it as the inspection model.") $minimumPrecision $heldoutLabelCount $minimumHeldoutLabelCount $minimumUiCandidateCount $candidateUiCandidateCount $positiveSegmentationLabelLineCount $minimumPositiveSegmentationLabelLineCount $positiveSegmentationImageCount $minimumPositiveSegmentationImageCount $segmentationOperatingEvidence
    }

    return New-PromotionRecommendationResult "review" @("Candidate metrics are mixed; inspect comparison examples before deciding.") $minimumPrecision $heldoutLabelCount $minimumHeldoutLabelCount $minimumUiCandidateCount $candidateUiCandidateCount $positiveSegmentationLabelLineCount $minimumPositiveSegmentationLabelLineCount $positiveSegmentationImageCount $minimumPositiveSegmentationImageCount $segmentationOperatingEvidence
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
    $lines.Add("- Image size: ``$($Summary.imageSize)``")
    $lines.Add("- Validation batch: ``$($Summary.batchSize)``")
    $lines.Add("- Benchmark repeats: ``$($Summary.benchmarkRepeatCount)``")
    $lines.Add("- UI confidence: ``$($Summary.uiConfidence)``")
    if ($null -ne $evidence) {
        if ($Summary.comparisonKind -ieq "engine-benchmark" -and $Summary.task -ieq "val") {
            $lines.Add("- Comparison evidence: ``$($evidence.comparisonLabelCount)`` labeled images from training validation (val); not model-replacement evidence")
        }
        else {
            $lines.Add("- Held-out evidence: ``$($evidence.comparisonLabelCount)`` labeled images / recommended ``$($evidence.recommendedLabelCount)``")
        }
        if ($null -ne $evidence.segmentationPositiveClassName) {
            $lines.Add("- Segmentation positive class: ``$($evidence.segmentationPositiveClassName)`` (id ``$($evidence.segmentationPositiveClassId)``)")
        }
        if ($null -ne $evidence.backgroundSegmentationImageCount) {
            $lines.Add("- Background segmentation images: ``$($evidence.backgroundSegmentationImageCount)``")
        }
    }
    $lines.Add("")
    $lines.Add("| Model | Engine | Precision | Recall | mAP50 | mAP50-95 | Model Takt median ms/image | Takt range ms/image | Inference median ms/image | UI candidates | Max confidence |")
    $lines.Add("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
    foreach ($item in @($baseline, $candidate)) {
        $metrics = $item.metrics
        $confidence = $item.confidence
        $taktRange = "$(Format-NullableNumber (Get-ModelBenchmarkValue $item 'taktMinMs'))-$(Format-NullableNumber (Get-ModelBenchmarkValue $item 'taktMaxMs'))"
        $lines.Add("| $($item.name) | $($item.engine) | $(Format-NullableNumber $metrics.precision) | $(Format-NullableNumber $metrics.recall) | $(Format-NullableNumber $metrics.map50) | $(Format-NullableNumber $metrics.map5095) | $(Format-NullableNumber (Get-ModelBenchmarkValue $item 'taktMs')) | $taktRange | $(Format-NullableNumber (Get-ModelBenchmarkValue $item 'inferenceMs')) | $($confidence.uiCandidateCount)/$($confidence.predictionCount) | $(Format-NullableNumber $confidence.maxConfidence) |")
    }
    $lines.Add("")
    $lines.Add("- Model Takt is the median native validation preprocess + inference + postprocess per image across the requested repeats. It does not include WPF, TCP, camera, PLC, or equipment cycle time.")
    if ($null -ne $candidate.confidence -and $null -ne $candidate.confidence.thresholdSweep) {
        $sweepItems = @()
        foreach ($item in $candidate.confidence.thresholdSweep) {
            $sweepItems += "``$(Format-NullableNumber $item.confidence): $($item.uiCandidateCount)``"
        }

        if ($sweepItems.Count -gt 0) {
            $lines.Add("")
            $lines.Add("- Candidate review threshold sweep: " + ($sweepItems -join ", "))
        }
    }
    if ($null -ne $candidate.confidence.uiPositiveImageCoverage) {
        $lines.Add("- Candidate UI positive-image coverage: ``$($candidate.confidence.uiPositiveImagesWithCandidates)/$($candidate.confidence.uiPositiveImageCount)`` (``$(Format-NullableNumber $candidate.confidence.uiPositiveImageCoverage)``)")
    }
    if ($null -ne $candidate.confidence.uiBackgroundCandidateRate) {
        $lines.Add("- Candidate UI background-candidate rate: ``$($candidate.confidence.uiBackgroundImagesWithCandidates)/$($candidate.confidence.uiBackgroundImageCount)`` (``$(Format-NullableNumber $candidate.confidence.uiBackgroundCandidateRate)``)")
    }
    $lines.Add("")
    if ($null -ne $promotion) {
        $isBenchmarkOnly = $promotion.recommendation -ieq "benchmark"
        $lines.Add($(if ($isBenchmarkOnly) { "## Engine Analysis" } else { "## Recommendation" }))
        $lines.Add("")
        $lines.Add("- Decision: ``$($promotion.recommendation)``")
        $promotionReasons = @()
        if ($null -ne $promotion.reasons) {
            foreach ($reason in $promotion.reasons) {
                if (-not [string]::IsNullOrWhiteSpace($reason)) {
                    $promotionReasons += $reason
                }
            }
        }
        if ($promotionReasons.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($promotion.reason)) {
            $promotionReasons += $promotion.reason
        }

        if ($promotionReasons.Count -le 1) {
            $lines.Add("- Reason: $($promotion.reason)")
        }
        else {
            $lines.Add("- Reasons:")
            foreach ($reason in $promotionReasons) {
                $lines.Add("  - $reason")
            }
        }
        if (-not $isBenchmarkOnly) {
            $lines.Add("- Minimum precision: ``$(Format-NullableNumber $promotion.minimumPrecision)``")
            $lines.Add("- Held-out labels: ``$($promotion.heldoutLabelCount)`` / required ``$($promotion.minimumHeldoutLabelCount)``")
            if ($null -ne $promotion.positiveSegmentationLabelLineCount) {
                $lines.Add("- Positive segmentation labels: ``$($promotion.positiveSegmentationLabelLineCount)`` / required ``$($promotion.minimumPositiveSegmentationLabelLineCount)``")
            }
            if ($null -ne $promotion.positiveSegmentationImageCount) {
                $lines.Add("- Positive segmentation images: ``$($promotion.positiveSegmentationImageCount)`` / required ``$($promotion.minimumPositiveSegmentationImageCount)``")
            }
            if ($null -ne $promotion.backgroundSegmentationImageCount) {
                $lines.Add("- Background segmentation images: ``$($promotion.backgroundSegmentationImageCount)`` / required ``$($promotion.minimumBackgroundSegmentationImageCount)``")
            }
            if ($null -ne $promotion.uiPositiveImageCoverage) {
                $lines.Add("- UI positive-image coverage: ``$(Format-NullableNumber $promotion.uiPositiveImageCoverage)`` / required ``$(Format-NullableNumber $promotion.minimumUiPositiveImageCoverage)``")
            }
            if ($null -ne $promotion.uiBackgroundCandidateRate) {
                $lines.Add("- UI background-candidate rate: ``$(Format-NullableNumber $promotion.uiBackgroundCandidateRate)`` / maximum ``$(Format-NullableNumber $promotion.maximumUiBackgroundCandidateRate)``")
            }
            if ($null -ne $promotion.uiCandidateCount) {
                $lines.Add("- UI candidates: ``$($promotion.uiCandidateCount)`` / required ``$($promotion.minimumUiCandidateCount)``")
            }
        }
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
if ([string]::IsNullOrWhiteSpace($BaselinePythonExe)) { $BaselinePythonExe = $PythonExe }
if ([string]::IsNullOrWhiteSpace($BaselineYoloSourceRoot)) { $BaselineYoloSourceRoot = $YoloSourceRoot }
if ([string]::IsNullOrWhiteSpace($CandidatePythonExe)) { $CandidatePythonExe = $PythonExe }
if ([string]::IsNullOrWhiteSpace($CandidateYoloSourceRoot)) { $CandidateYoloSourceRoot = $YoloSourceRoot }

$PythonExe = Resolve-PathValue $PythonExe
$YoloProjectRoot = Resolve-PathValue $YoloProjectRoot
$YoloSourceRoot = Resolve-PathValue $YoloSourceRoot
$BaselinePythonExe = Resolve-PathValue $BaselinePythonExe
$BaselineYoloSourceRoot = Resolve-PathValue $BaselineYoloSourceRoot
$CandidatePythonExe = Resolve-PathValue $CandidatePythonExe
$CandidateYoloSourceRoot = Resolve-PathValue $CandidateYoloSourceRoot
$DataYaml = Resolve-PathValue $DataYaml
$BaselineWeights = Resolve-PathValue $BaselineWeights
$CandidateWeights = Resolve-PathValue $CandidateWeights
$OutputDirectory = Resolve-PathValue $OutputDirectory

Assert-File $BaselinePythonExe "Baseline Python executable"
Assert-Directory $BaselineYoloSourceRoot "Baseline YOLO source root"
Assert-YoloValidationRuntime $BaselineYoloSourceRoot "Baseline YOLO"
Assert-File $CandidatePythonExe "Candidate Python executable"
Assert-Directory $CandidateYoloSourceRoot "Candidate YOLO source root"
Assert-YoloValidationRuntime $CandidateYoloSourceRoot "Candidate YOLO"
Assert-File $DataYaml "YOLO data.yaml"
Assert-File $BaselineWeights "Baseline weights"
Assert-File $CandidateWeights "Candidate weights"
$BaselineEngine = Resolve-EngineName $BaselineEngine $BaselineYoloSourceRoot
$CandidateEngine = Resolve-EngineName $CandidateEngine $CandidateYoloSourceRoot
$dataClassInfo = Assert-ModelClassCountsMatchData
$segmentationPositiveClass = Resolve-SegmentationPositiveClass $dataClassInfo $SegmentationPositiveClassName $ModelTask
$script:SegmentationPositiveClassId = if ($null -eq $segmentationPositiveClass) { $null } else { [int]$segmentationPositiveClass.id }

$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runOutputRoot = Join-Path $OutputDirectory $stamp
New-Item -ItemType Directory -Force -Path $runOutputRoot | Out-Null

$baseline = Invoke-ModelValWithBenchmarkRepeats "baseline" $BaselineWeights $runOutputRoot $BaselinePythonExe $BaselineYoloSourceRoot $BaselineEngine $BenchmarkRepeatCount
$candidate = Invoke-ModelValWithBenchmarkRepeats "candidate" $CandidateWeights $runOutputRoot $CandidatePythonExe $CandidateYoloSourceRoot $CandidateEngine $BenchmarkRepeatCount
$evidence = New-ComparisonEvidence $DataYaml $Task $ModelTask $segmentationPositiveClass
if ($ModelTask -ieq "segment") {
    Add-SegmentationPredictionImageEvidence $baseline $evidence $UiConfidence
    Add-SegmentationPredictionImageEvidence $candidate $evidence $UiConfidence
}
$promotion = New-PromotionRecommendation $baseline $candidate $evidence $UiConfidence
$comparisonKind = if ($BaselineEngine -ine $CandidateEngine) { "engine-benchmark" } else { "candidate-validation" }
if ($comparisonKind -ieq "engine-benchmark" -and $Task -ieq "val") {
    $benchmarkReason = "Training validation (val) results are for engine performance analysis and are not model-replacement evidence."
    $promotion["recommendation"] = "benchmark"
    $promotion["reason"] = $benchmarkReason
    $promotion["reasons"] = @($benchmarkReason)
}

$summary = [ordered]@{
    createdAt = (Get-Date).ToString("o")
    dataYaml = $DataYaml
    task = $Task
    modelTask = $ModelTask
    comparisonKind = $comparisonKind
    imageSize = $ImageSize
    batchSize = $BatchSize
    benchmarkRepeatCount = $BenchmarkRepeatCount
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
