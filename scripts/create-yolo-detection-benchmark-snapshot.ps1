param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDatasetRoot,
    [Parameter(Mandatory = $true)]
    [string]$OutputRoot,
    [int]$Seed = 20260716,
    [int]$ValidationImageCount = 20,
    [int]$TestImageCount = 20,
    [int]$ValidationNgImageCount = 3,
    [int]$TestNgImageCount = 5,
    [int]$NgClassId = 1
)

$ErrorActionPreference = "Stop"

function Get-FileSha256([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

function Get-TextSha256([string[]]$Lines) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]::Join("`n", @($Lines)))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.UTF8Encoding]::new($false))
}

function Read-ClassNames([string]$DataYamlPath) {
    $lines = @(Get-Content -LiteralPath $DataYamlPath)
    $namesStart = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^\s*names\s*:\s*$') {
            $namesStart = $index
            break
        }
    }

    if ($namesStart -lt 0) {
        throw "data.yaml must use a block names list: $DataYamlPath"
    }

    $names = @()
    for ($index = $namesStart + 1; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match '^\s*-\s*(.+?)\s*$') {
            $names += $matches[1].Trim('"', "'")
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($lines[$index])) {
            break
        }
    }

    if ($names.Count -eq 0) {
        throw "data.yaml has no class names: $DataYamlPath"
    }

    return $names
}

function Get-DeterministicOrderKey([int]$RandomSeed, [string]$Value) {
    return Get-TextSha256 @("$RandomSeed|$($Value.ToLowerInvariant())")
}

function Set-StratumSplits([object[]]$Records, [int]$ValidationCount, [int]$TestCount, [string]$Name) {
    if ($ValidationCount -lt 0 -or $TestCount -lt 0) {
        throw "$Name validation/test counts cannot be negative."
    }

    if ($Records.Count -le ($ValidationCount + $TestCount)) {
        throw "$Name requires at least one training image after holdout; available=$($Records.Count), validation=$ValidationCount, test=$TestCount."
    }

    $ordered = @($Records | Sort-Object OrderKey, Stem)
    for ($index = 0; $index -lt $ordered.Count; $index++) {
        if ($index -lt $TestCount) {
            $ordered[$index].TargetSplit = "test"
        }
        elseif ($index -lt ($TestCount + $ValidationCount)) {
            $ordered[$index].TargetSplit = "valid"
        }
        else {
            $ordered[$index].TargetSplit = "train"
        }
    }
}

$sourceRoot = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($SourceDatasetRoot))
$outputPath = [System.IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($OutputRoot))
$sourceDataYaml = Join-Path $sourceRoot "data.yaml"
if (-not (Test-Path -LiteralPath $sourceDataYaml -PathType Leaf)) {
    throw "Source data.yaml was not found: $sourceDataYaml"
}

if (Test-Path -LiteralPath $outputPath) {
    throw "OutputRoot already exists. Use a new versioned path: $outputPath"
}

if ($ValidationImageCount -lt $ValidationNgImageCount -or $TestImageCount -lt $TestNgImageCount) {
    throw "NG holdout counts cannot exceed total holdout image counts."
}

$classNames = @(Read-ClassNames $sourceDataYaml)
if ($NgClassId -lt 0 -or $NgClassId -ge $classNames.Count) {
    throw "NgClassId $NgClassId is outside the data.yaml class range 0..$($classNames.Count - 1)."
}

$imageExtensions = @(".bmp", ".jpeg", ".jpg", ".png", ".tif", ".tiff", ".webp")
$records = @()
foreach ($sourceSplit in @("train", "valid", "test")) {
    $imageRoot = Join-Path $sourceRoot "data\$sourceSplit\images"
    $labelRoot = Join-Path $sourceRoot "data\$sourceSplit\labels"
    if (-not (Test-Path -LiteralPath $imageRoot -PathType Container)) {
        continue
    }

    foreach ($image in Get-ChildItem -LiteralPath $imageRoot -File | Where-Object { $_.Extension.ToLowerInvariant() -in $imageExtensions }) {
        $labelPath = Join-Path $labelRoot ($image.BaseName + ".txt")
        if (-not (Test-Path -LiteralPath $labelPath -PathType Leaf)) {
            throw "Missing YOLO label for $($image.FullName): $labelPath"
        }

        $classIds = @()
        $objectCount = 0
        foreach ($line in Get-Content -LiteralPath $labelPath) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            if ($line -notmatch '^\s*(\d+)(?:\s|$)') {
                throw "Invalid YOLO label line in ${labelPath}: $line"
            }

            $classId = [int]$matches[1]
            if ($classId -lt 0 -or $classId -ge $classNames.Count) {
                throw "Class id $classId is outside the data.yaml class range in $labelPath."
            }

            $classIds += $classId
            $objectCount++
        }

        $records += [pscustomobject]@{
            Stem = $image.BaseName
            FileName = $image.Name
            SourceSplit = $sourceSplit
            SourceImagePath = $image.FullName
            SourceLabelPath = $labelPath
            ImageSha256 = Get-FileSha256 $image.FullName
            LabelSha256 = Get-FileSha256 $labelPath
            ObjectCount = $objectCount
            ClassIds = @($classIds)
            HasNg = $classIds -contains $NgClassId
            OrderKey = Get-DeterministicOrderKey $Seed $image.BaseName
            TargetSplit = ""
        }
    }
}

if ($records.Count -eq 0) {
    throw "No labeled images were found under $sourceRoot."
}

$duplicateStems = @($records | Group-Object Stem | Where-Object Count -gt 1)
if ($duplicateStems.Count -gt 0) {
    throw "Duplicate image stems would collide with YOLO labels: $([string]::Join(', ', @($duplicateStems | ForEach-Object Name)))"
}

$duplicateImages = @($records | Group-Object ImageSha256 | Where-Object Count -gt 1)
if ($duplicateImages.Count -gt 0) {
    $names = @($duplicateImages | ForEach-Object { [string]::Join('/', @($_.Group | ForEach-Object FileName)) })
    throw "Duplicate image content could leak across splits: $([string]::Join(', ', $names))"
}

$ngRecords = @($records | Where-Object HasNg)
$normalRecords = @($records | Where-Object { -not $_.HasNg })
$validationNormalCount = $ValidationImageCount - $ValidationNgImageCount
$testNormalCount = $TestImageCount - $TestNgImageCount
Set-StratumSplits $ngRecords $ValidationNgImageCount $TestNgImageCount "NG-containing stratum"
Set-StratumSplits $normalRecords $validationNormalCount $testNormalCount "non-NG stratum"

foreach ($split in @("train", "valid", "test")) {
    New-Item -ItemType Directory -Path (Join-Path $outputPath "data\$split\images") -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $outputPath "data\$split\labels") -Force | Out-Null
}

foreach ($record in $records) {
    $targetImage = Join-Path $outputPath "data\$($record.TargetSplit)\images\$($record.FileName)"
    $targetLabel = Join-Path $outputPath "data\$($record.TargetSplit)\labels\$($record.Stem).txt"
    Copy-Item -LiteralPath $record.SourceImagePath -Destination $targetImage
    Copy-Item -LiteralPath $record.SourceLabelPath -Destination $targetLabel
}

$yamlLines = @(
    "train: $((Join-Path $outputPath 'data\train\images').Replace('\', '/'))"
    "val: $((Join-Path $outputPath 'data\valid\images').Replace('\', '/'))"
    "test: $((Join-Path $outputPath 'data\test\images').Replace('\', '/'))"
    "nc: $($classNames.Count)"
    "names:"
) + @($classNames | ForEach-Object { "- $_" })
$targetDataYaml = Join-Path $outputPath "data.yaml"
Write-Utf8NoBom $targetDataYaml ([string]::Join("`n", $yamlLines) + "`n")

$splitSummaries = @()
foreach ($split in @("train", "valid", "test")) {
    $splitRecords = @($records | Where-Object TargetSplit -eq $split)
    $splitFingerprint = Get-TextSha256 @($splitRecords | ForEach-Object { "$($_.ImageSha256)|$($_.LabelSha256)" } | Sort-Object)
    $classObjectCounts = [ordered]@{}
    for ($classId = 0; $classId -lt $classNames.Count; $classId++) {
        $classObjectCounts["$classId`:$($classNames[$classId])"] = @($splitRecords | ForEach-Object ClassIds | Where-Object { $_ -eq $classId }).Count
    }

    $splitSummaries += [ordered]@{
        split = $split
        imageCount = $splitRecords.Count
        ngImageCount = @($splitRecords | Where-Object HasNg).Count
        objectCount = ($splitRecords | Measure-Object ObjectCount -Sum).Sum
        classObjectCounts = $classObjectCounts
        fingerprintAlgorithm = "sha256-image-label-pairs-v1"
        fingerprintSha256 = $splitFingerprint
    }
}

$datasetFingerprint = Get-TextSha256 @($records | ForEach-Object { "$($_.ImageSha256)|$($_.LabelSha256)" } | Sort-Object)
$manifest = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTime]::UtcNow.ToString("o")
    datasetVersionId = "yolo-detect-$($datasetFingerprint.Substring(0, 16))-seed-$Seed"
    sourceDatasetRoot = $sourceRoot
    sourceDataYamlSha256 = Get-FileSha256 $sourceDataYaml
    outputRoot = $outputPath
    dataYamlPath = $targetDataYaml
    dataYamlSha256 = Get-FileSha256 $targetDataYaml
    seed = $Seed
    ngClassId = $NgClassId
    classNames = $classNames
    sourceImageCount = $records.Count
    sourceNgImageCount = $ngRecords.Count
    fingerprintAlgorithm = "sha256-image-label-pairs-v1"
    fingerprintSha256 = $datasetFingerprint
    splits = $splitSummaries
    items = @($records | Sort-Object TargetSplit, FileName | ForEach-Object {
        [ordered]@{
            fileName = $_.FileName
            sourceSplit = $_.SourceSplit
            targetSplit = $_.TargetSplit
            hasNg = $_.HasNg
            objectCount = $_.ObjectCount
            classIds = $_.ClassIds
            imageSha256 = $_.ImageSha256
            labelSha256 = $_.LabelSha256
        }
    })
}

$manifestPath = Join-Path $outputPath "benchmark-dataset.manifest.json"
Write-Utf8NoBom $manifestPath (($manifest | ConvertTo-Json -Depth 8) + "`n")

[ordered]@{
    outputRoot = $outputPath
    dataYaml = $targetDataYaml
    manifest = $manifestPath
    datasetVersionId = $manifest.datasetVersionId
    fingerprintSha256 = $datasetFingerprint
    splits = $splitSummaries
} | ConvertTo-Json -Depth 6
