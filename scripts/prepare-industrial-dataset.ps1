param(
    [string]$WorkspaceRoot = "$env:USERPROFILE\LabelingIndustrialDatasets",
    [ValidateSet("KolektorSDD", "VisA", "Severstal", "Manual")]
    [string]$Dataset = "KolektorSDD",
    [string]$DownloadUrl = "",
    [string]$ArchivePath = "",
    [switch]$Download,
    [switch]$UseKaggle,
    [string]$KaggleDataset = "",
    [string]$KaggleOutputDir = "",
    [switch]$ForAppLayout,
    [switch]$IncludeLabelImages,
    [switch]$CreateYoloLabelsFromKolektorMasks,
    [double]$TrainSplitRatio = 1.0,
    [double]$TestSplitRatio = 0.0,
    [switch]$CreateDataYaml,
    [switch]$UseAbsoluteYamlPath,
    [switch]$CleanOutput,
    [int]$TrainPositiveOversampleFactor = 1,
    [string[]]$ClassNames = @("OK", "NG"),
    [int]$RandomSeed = 20260626
)

$ErrorActionPreference = "Stop"

function Get-ScriptRoot {
    # $PSScriptRoot is stable in Windows PowerShell and avoids function-local MyInvocation edge-cases.
    return $PSScriptRoot
}

function Resolve-Paths {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ""
    }

    $expanded = $Value.Replace('${scriptRoot}', (Get-ScriptRoot))
    $expanded = [Environment]::ExpandEnvironmentVariables($expanded)
    if ([System.IO.Path]::IsPathRooted($expanded)) {
        return [System.IO.Path]::GetFullPath($expanded)
    }

    return [System.IO.Path]::GetFullPath($expanded)
}

function Get-ImageFiles {
    param([string]$Root, [switch]$IncludeLabelImages)
    return Get-ChildItem -LiteralPath $Root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff" } |
        Where-Object { $IncludeLabelImages -or -not (Test-LabelImageName -Path $_.FullName) } |
        Sort-Object FullName
}

function Test-LabelImageName {
    param([string]$Path)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($Path)
    return $stem -match "(?i)(^|[_\-.])(label|labels|mask|masks|gt|groundtruth)$"
}

function New-FlatCopyName {
    param([System.IO.FileInfo]$SourceFile, [string]$SourceRoot, [string]$DestinationRoot)
    if ($SourceFile.FullName.Length -ge $SourceRoot.Length -and $SourceFile.FullName.StartsWith($SourceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $relative = $SourceFile.FullName.Substring($SourceRoot.Length)
    }
    else {
        $relative = [System.IO.Path]::GetFileName($SourceFile.FullName)
    }

    $relative = $relative.TrimStart([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $relative = $relative -replace "[\\/]", "_"
    if ($relative.Length -gt 200) {
        $relative = $relative.Substring($relative.Length - 200)
    }

    $candidate = Join-Path $DestinationRoot $relative
    if (-not (Test-Path -LiteralPath $candidate)) {
        return $candidate
    }

    $base = [System.IO.Path]::GetFileNameWithoutExtension($relative)
    $ext = [System.IO.Path]::GetExtension($relative)
    $index = 1
    while ($true) {
        $candidate = Join-Path $DestinationRoot ("{0}_{1:000}{2}" -f $base, $index, $ext)
        if (-not (Test-Path -LiteralPath $candidate)) {
            return $candidate
        }
        $index++
    }
}

function Copy-ImagesFlat {
    param([string]$DestinationRoot, [System.IO.FileInfo[]]$Images, [string]$SourceRoot, [switch]$PassThru)
    New-Item -ItemType Directory -Force -Path $DestinationRoot | Out-Null
    $copied = 0
    $records = @()
    foreach ($image in $Images) {
        $target = New-FlatCopyName -SourceFile $image -SourceRoot $SourceRoot -DestinationRoot $DestinationRoot
        Copy-Item -LiteralPath $image.FullName -Destination $target -Force
        $copied++
        if ($PassThru) {
            $records += [pscustomobject]@{
                SourcePath = $image.FullName
                TargetPath = $target
                TargetStem = [System.IO.Path]::GetFileNameWithoutExtension($target)
            }
        }
    }

    if ($PassThru) {
        return $records
    }

    return $copied
}

function Split-TrainValidTest {
    param([System.IO.FileInfo[]]$Images, [double]$TrainRatio, [double]$TestRatio)
    if ($Images.Count -eq 0) {
        return @{ Train = @(); Valid = @(); Test = @() }
    }

    $random = [System.Random]::new($RandomSeed)
    $shuffled = $Images | ForEach-Object { $_ }
    for ($i = $shuffled.Count - 1; $i -gt 0; $i--) {
        $j = $random.Next($i + 1)
        $temp = $shuffled[$i]
        $shuffled[$i] = $shuffled[$j]
        $shuffled[$j] = $temp
    }

    $testCount = 0
    if ($TestRatio -gt 0.0 -and $Images.Count -ge 3) {
        $testCount = [Math]::Max(1, [Math]::Floor($TestRatio * $Images.Count))
        $testCount = [Math]::Min($testCount, $Images.Count - 2)
    }

    $trainAndValidCount = $Images.Count - $testCount
    $testItems = if ($testCount -gt 0) { $shuffled[($Images.Count - $testCount)..($Images.Count - 1)] } else { @() }
    $trainAndValidItems = if ($trainAndValidCount -gt 0) { $shuffled[0..($trainAndValidCount - 1)] } else { @() }

    if ($TrainRatio -ge 1.0 -or $TrainRatio -le 0.0 -or $trainAndValidItems.Count -eq 0) {
        return @{ Train = $trainAndValidItems; Valid = @(); Test = $testItems }
    }

    $trainCount = [Math]::Max(1, [Math]::Floor(($TrainRatio * $trainAndValidItems.Count) + 0.000001))
    return @{
        Train = $trainAndValidItems[0..($trainCount - 1)]
        Valid = if ($trainCount -lt $trainAndValidItems.Count) { $trainAndValidItems[$trainCount..($trainAndValidItems.Count - 1)] } else { @() }
        Test = $testItems
    }
}

function Get-StringPath {
    param([object]$InputObject)
    if ($null -eq $InputObject) {
        return ""
    }

    if ($InputObject -is [string]) {
        return $InputObject
    }

    if ($InputObject -is [System.IO.FileSystemInfo]) {
        return $InputObject.FullName
    }

    # PathInfo from Resolve-Path on Windows PowerShell doesn't expose FullName, but Path is available.
    if ($InputObject.PSObject.Properties.Match("Path").Count -gt 0) {
        return [string]$InputObject.Path
    }

    return ""
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Text)
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Write-DataYaml {
    param([string]$Path, [string]$DatasetPath, [string]$TrainImagePath, [string]$ValidImagePath, [string]$TestImagePath, [string[]]$Names)
    $hasValid = (-not [string]::IsNullOrWhiteSpace($ValidImagePath))
    $hasTest = (-not [string]::IsNullOrWhiteSpace($TestImagePath))
    $yaml = "path: $DatasetPath`n"
    $yaml += "train: $TrainImagePath`n"
    if ($hasValid) {
        $yaml += "val: $ValidImagePath`n"
    }

    if ($hasTest) {
        $yaml += "test: $TestImagePath`n"
    }

    $yaml += "nc: $($Names.Count)`n"
    $yaml += "names:`n"
    foreach ($name in $Names) {
        $yaml += "  - $name`n"
    }

    Write-Utf8NoBom -Path $Path -Text $yaml
}

function Find-KolektorMaskPath {
    param([string]$ImagePath)
    $directory = [System.IO.Path]::GetDirectoryName($ImagePath)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($ImagePath)
    $candidate = Join-Path $directory ($stem + "_label.bmp")
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return $candidate
    }

    return ""
}

function Ensure-KolektorMaskBoxReader {
    if ("KolektorMaskBoxReader" -as [type]) {
        return
    }

    Add-Type -ReferencedAssemblies "System.Drawing" -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class KolektorMaskBoxReader
{
    public static int[] ReadBoundingBox(string maskPath, int foregroundThreshold)
    {
        using (var source = new Bitmap(maskPath))
        using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.DrawImageUnscaled(source, 0, 0);
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                byte[] bytes = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);

                int minX = bitmap.Width;
                int minY = bitmap.Height;
                int maxX = -1;
                int maxY = -1;
                for (int y = 0; y < bitmap.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int offset = row + (x * 3);
                        byte blue = bytes[offset];
                        byte green = bytes[offset + 1];
                        byte red = bytes[offset + 2];
                        if (Math.Max(red, Math.Max(green, blue)) > foregroundThreshold)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (maxX < minX || maxY < minY)
                {
                    return null;
                }

                return new[] { minX, minY, maxX - minX + 1, maxY - minY + 1, bitmap.Width, bitmap.Height };
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }
}
"@
}

function Get-MaskBoundingBox {
    param([string]$MaskPath, [int]$ForegroundThreshold = 15)
    Ensure-KolektorMaskBoxReader
    $values = [KolektorMaskBoxReader]::ReadBoundingBox($MaskPath, $ForegroundThreshold)
    if ($null -eq $values) {
        return $null
    }

    return [pscustomobject]@{
        X = $values[0]
        Y = $values[1]
        Width = $values[2]
        Height = $values[3]
        ImageWidth = $values[4]
        ImageHeight = $values[5]
    }
}

function Write-YoloLabelsFromKolektorMasks {
    param([object[]]$Records, [string]$LabelRoot)
    New-Item -ItemType Directory -Force -Path $LabelRoot | Out-Null
    $written = 0
    $defect = 0
    $empty = 0
    foreach ($record in $Records) {
        $targetLabelPath = Join-Path $LabelRoot ($record.TargetStem + ".txt")
        $maskPath = Find-KolektorMaskPath -ImagePath $record.SourcePath
        if ([string]::IsNullOrWhiteSpace($maskPath)) {
            Write-Utf8NoBom -Path $targetLabelPath -Text ""
            $empty++
            $written++
            continue
        }

        $box = Get-MaskBoundingBox -MaskPath $maskPath
        if ($null -eq $box) {
            Write-Utf8NoBom -Path $targetLabelPath -Text ""
            $empty++
            $written++
            continue
        }

        $centerX = ($box.X + ($box.Width / 2.0)) / $box.ImageWidth
        $centerY = ($box.Y + ($box.Height / 2.0)) / $box.ImageHeight
        $width = $box.Width / $box.ImageWidth
        $height = $box.Height / $box.ImageHeight
        $line = [string]::Format(
            [System.Globalization.CultureInfo]::InvariantCulture,
            "0 {0:0.######} {1:0.######} {2:0.######} {3:0.######}`n",
            $centerX,
            $centerY,
            $width,
            $height)
        Write-Utf8NoBom -Path $targetLabelPath -Text $line
        $defect++
        $written++
    }

    return [pscustomobject]@{
        Written = $written
        Defect = $defect
        Empty = $empty
    }
}

function Get-YoloLabelStats {
    param([string]$LabelRoot)
    if (-not (Test-Path -LiteralPath $LabelRoot -PathType Container)) {
        return [pscustomobject]@{
            Written = 0
            Defect = 0
            Empty = 0
        }
    }

    $labels = @(Get-ChildItem -LiteralPath $LabelRoot -File -Filter "*.txt")
    $positive = @($labels | Where-Object { $_.Length -gt 0 })
    return [pscustomobject]@{
        Written = $labels.Count
        Defect = $positive.Count
        Empty = $labels.Count - $positive.Count
    }
}

function Find-MatchingTrainImage {
    param([string]$ImageRoot, [string]$Stem)
    foreach ($extension in ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff") {
        $candidate = Join-Path $ImageRoot ($Stem + $extension)
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    return ""
}

function Add-PositiveTrainingCopies {
    param([string]$ImageRoot, [string]$LabelRoot, [int]$Factor)
    if ($Factor -le 1 -or -not (Test-Path -LiteralPath $LabelRoot -PathType Container)) {
        return [pscustomobject]@{
            SourcePositive = 0
            Added = 0
        }
    }

    $positiveLabels = @(Get-ChildItem -LiteralPath $LabelRoot -File -Filter "*.txt" | Where-Object { $_.Length -gt 0 })
    $added = 0
    foreach ($label in $positiveLabels) {
        $sourceStem = [System.IO.Path]::GetFileNameWithoutExtension($label.Name)
        $sourceImage = Find-MatchingTrainImage -ImageRoot $ImageRoot -Stem $sourceStem
        if ([string]::IsNullOrWhiteSpace($sourceImage)) {
            continue
        }

        $extension = [System.IO.Path]::GetExtension($sourceImage)
        for ($copyIndex = 2; $copyIndex -le $Factor; $copyIndex++) {
            $targetStem = "{0}_pos{1:00}" -f $sourceStem, $copyIndex
            $targetImage = Join-Path $ImageRoot ($targetStem + $extension)
            $targetLabel = Join-Path $LabelRoot ($targetStem + ".txt")
            Copy-Item -LiteralPath $sourceImage -Destination $targetImage -Force
            Copy-Item -LiteralPath $label.FullName -Destination $targetLabel -Force
            $added++
        }
    }

    return [pscustomobject]@{
        SourcePositive = $positiveLabels.Count
        Added = $added
    }
}

function Download-Kolektor {
    param([string]$TargetDir)
    $defaultUrl = "https://go.vicos.si/kolektorsddboxes"
    if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
        $DownloadUrl = $defaultUrl
    }

    $archivePath = Join-Path $TargetDir "kolektor_sdd.zip"
    Write-Host "Downloading KolektorSDD from $DownloadUrl"
    Invoke-WebRequest -Uri $DownloadUrl -OutFile $archivePath -MaximumRedirection 10
    return $archivePath
}

function Download-Visa {
    param([string]$TargetDir)
    $defaultKaggle = "tensura3607/amazon-visa-anomaly"
    if (-not $UseKaggle) {
        throw "VisA dataset is not hosted as direct zip in this script. Re-run with -UseKaggle or provide Manual source."
    }

    if ([string]::IsNullOrWhiteSpace($KaggleDataset)) {
        $KaggleDataset = $defaultKaggle
    }

    $command = Get-Command "kaggle" -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Kaggle CLI not found. Install kaggle package and authenticate (kaggle.json), or use -Dataset Manual with a zip path."
    }

    if ([string]::IsNullOrWhiteSpace($KaggleOutputDir)) {
        $KaggleOutputDir = $TargetDir
    }

    Write-Host "Downloading VisA from Kaggle dataset '$KaggleDataset'"
    & kaggle datasets download -d $KaggleDataset -p $KaggleOutputDir --unzip
    if ($LASTEXITCODE -ne 0) {
        throw "kaggle datasets download failed with exit code $LASTEXITCODE"
    }
}

function Download-Severstal {
    param([string]$TargetDir)
    $defaultUrl = "https://www.kaggle.com/c/severstal-steel-defect-detection/data"
    if (-not $UseKaggle) {
        throw "Severstal dataset is distributed on Kaggle in practice. Re-run with -UseKaggle and -Dataset Severstal."
    }

    if ([string]::IsNullOrWhiteSpace($KaggleDataset)) {
        $KaggleDataset = "paultimothymooney/severstal-steel-defect-detection"
    }

    $command = Get-Command "kaggle" -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "Kaggle CLI not found. Install kaggle package and authenticate (kaggle.json)."
    }

    Write-Host "Downloading Severstal from Kaggle dataset '$KaggleDataset'"
    if ([string]::IsNullOrWhiteSpace($KaggleOutputDir)) {
        $KaggleOutputDir = $TargetDir
    }

    & kaggle competitions download -c severstal-steel-defect-detection -p $KaggleOutputDir
    if ($LASTEXITCODE -ne 0) {
        throw "kaggle competition download failed with exit code $LASTEXITCODE"
    }
}

function Import-KnownDataset {
    param([string]$TargetDir)
    switch ($Dataset) {
        "KolektorSDD" {
            return Download-Kolektor -TargetDir $TargetDir
        }
        "VisA" {
            Download-Visa -TargetDir $TargetDir
            return ""
        }
        "Severstal" {
            Download-Severstal -TargetDir $TargetDir
            return ""
        }
        default {
            throw "Unknown dataset: $Dataset"
        }
    }
}

$workspaceRootPath = [string]$WorkspaceRoot
$workspaceRootObject = Resolve-Path -LiteralPath $workspaceRootPath -ErrorAction SilentlyContinue
if ($null -eq $workspaceRootObject) {
    New-Item -ItemType Directory -Force -Path $workspaceRootPath | Out-Null
    $workspaceRootObject = Get-Item -LiteralPath $workspaceRootPath
}
$workspaceRoot = Get-StringPath -InputObject $workspaceRootObject
if ([string]::IsNullOrWhiteSpace($workspaceRoot)) {
    throw "Could not resolve workspace root: $WorkspaceRoot"
}
$datasetRoot = Join-Path $workspaceRoot $Dataset
$rawRoot = Join-Path $datasetRoot "raw"
$preparedRoot = Join-Path $datasetRoot "prepared"
$appRoot = Join-Path $datasetRoot "app"
$rawZipPath = ""
$manualRoot = ""

New-Item -ItemType Directory -Force -Path $rawRoot, $preparedRoot | Out-Null

if ($Download) {
    $rawZipPath = Import-KnownDataset -TargetDir $rawRoot
}

if (-not [string]::IsNullOrWhiteSpace($ArchivePath)) {
    $resolvedArchivePath = Resolve-Paths $ArchivePath
    if (Test-Path -LiteralPath $resolvedArchivePath -PathType Container) {
        $manualRoot = $resolvedArchivePath
    }
    elseif (Test-Path -LiteralPath $resolvedArchivePath -PathType Leaf) {
        $rawZipPath = $resolvedArchivePath
    }
    else {
        throw "ArchivePath does not exist: $resolvedArchivePath"
    }
}

if (-not [string]::IsNullOrWhiteSpace($rawZipPath) -and (Test-Path -LiteralPath $rawZipPath -PathType Leaf)) {
    $downloadFile = [System.IO.Path]::GetExtension($rawZipPath).ToLowerInvariant()
    if ($downloadFile -eq ".zip") {
        $expanded = Join-Path $rawRoot "expanded"
        New-Item -ItemType Directory -Force -Path $expanded | Out-Null
        Expand-Archive -Path $rawZipPath -DestinationPath $expanded -Force
        Write-Host "Expanded archive to $expanded"
    }
    else {
        throw "ArchivePath file must be a .zip archive: $rawZipPath"
    }
}

$rawImagesDir = Join-Path $rawRoot "expanded"
$rawContainerCandidates = @()
if (-not (Test-Path -LiteralPath $rawImagesDir -PathType Container)) {
    if (-not [string]::IsNullOrWhiteSpace($manualRoot) -and (Test-Path -LiteralPath $manualRoot -PathType Container)) {
        $rawImagesDir = $manualRoot
    }
    elseif (Test-Path -LiteralPath $rawRoot -PathType Container) {
        $rawContainerCandidates = Get-ChildItem -LiteralPath $rawRoot -Directory -ErrorAction SilentlyContinue
        if ($rawContainerCandidates.Count -eq 1) {
            $rawImagesDir = $rawContainerCandidates[0].FullName
            Write-Host "Detected single root folder in raw data. Using '$rawImagesDir'."
        }
        else {
            $rawImagesDir = $rawRoot
        }
    }
    else {
        throw "Raw image directory not found: $rawImagesDir"
    }
}

# Fallback for download/archive-only runs where a dataset folder lands under raw\expanded\*
if ($rawImagesDir -eq (Join-Path $rawRoot "expanded") -and -not (Test-Path -LiteralPath $rawImagesDir -PathType Container)) {
    if (Test-Path -LiteralPath $rawRoot -PathType Container) {
        $rawImagesDir = $rawRoot
    }
}

$images = Get-ImageFiles -Root $rawImagesDir -IncludeLabelImages:$IncludeLabelImages
if ($images.Count -eq 0) {
    throw "No image files found under: $rawImagesDir"
}

$effectiveClassNames = $ClassNames
if ($CreateYoloLabelsFromKolektorMasks -and $ClassNames.Count -eq 2 -and $ClassNames[0] -eq "OK" -and $ClassNames[1] -eq "NG") {
    # Kolektor mask boxes represent defect objects. Normal images are written as empty YOLO txt files, not as an OK object class.
    $effectiveClassNames = @("Defect")
}

$splits = Split-TrainValidTest -Images $images -TrainRatio $TrainSplitRatio -TestRatio $TestSplitRatio
$trainImages = $splits.Train
$validImages = $splits.Valid
$testImages = $splits.Test

if ($ForAppLayout) {
    if ($CleanOutput) {
        $appData = Join-Path $appRoot "data"
        if (Test-Path -LiteralPath $appData) {
            Remove-Item -LiteralPath $appData -Recurse -Force
        }

        $existingYaml = Join-Path $appRoot "data.yaml"
        if (Test-Path -LiteralPath $existingYaml) {
            Remove-Item -LiteralPath $existingYaml -Force
        }
    }

    $appTrain = Join-Path $appRoot "data\train\images"
    $appValid = Join-Path $appRoot "data\valid\images"
    $appTest = Join-Path $appRoot "data\test\images"
    $appDataYaml = Join-Path $appRoot "data.yaml"

    $trainRecords = @(Copy-ImagesFlat -DestinationRoot $appTrain -Images $trainImages -SourceRoot $rawImagesDir -PassThru)
    $trainCopied = $trainRecords.Count
    $validCopied = 0
    $validRecords = @()
    if ($validImages.Count -gt 0) {
        $validRecords = @(Copy-ImagesFlat -DestinationRoot $appValid -Images $validImages -SourceRoot $rawImagesDir -PassThru)
        $validCopied = $validRecords.Count
    }

    $testCopied = 0
    $testRecords = @()
    if ($testImages.Count -gt 0) {
        $testRecords = @(Copy-ImagesFlat -DestinationRoot $appTest -Images $testImages -SourceRoot $rawImagesDir -PassThru)
        $testCopied = $testRecords.Count
    }

    $trainLabels = $null
    $validLabels = $null
    $testLabels = $null
    if ($CreateYoloLabelsFromKolektorMasks) {
        $trainLabels = Write-YoloLabelsFromKolektorMasks -Records $trainRecords -LabelRoot (Join-Path $appRoot "data\train\labels")
        if ($validRecords.Count -gt 0) {
            $validLabels = Write-YoloLabelsFromKolektorMasks -Records $validRecords -LabelRoot (Join-Path $appRoot "data\valid\labels")
        }

        if ($testRecords.Count -gt 0) {
            $testLabels = Write-YoloLabelsFromKolektorMasks -Records $testRecords -LabelRoot (Join-Path $appRoot "data\test\labels")
        }
    }

    $oversampleResult = Add-PositiveTrainingCopies -ImageRoot $appTrain -LabelRoot (Join-Path $appRoot "data\train\labels") -Factor $TrainPositiveOversampleFactor
    $trainCopied = @(Get-ChildItem -LiteralPath $appTrain -File -ErrorAction SilentlyContinue).Count
    $validCopied = if (Test-Path -LiteralPath $appValid -PathType Container) { @(Get-ChildItem -LiteralPath $appValid -File -ErrorAction SilentlyContinue).Count } else { 0 }
    $testCopied = if (Test-Path -LiteralPath $appTest -PathType Container) { @(Get-ChildItem -LiteralPath $appTest -File -ErrorAction SilentlyContinue).Count } else { 0 }
    $trainLabels = Get-YoloLabelStats -LabelRoot (Join-Path $appRoot "data\train\labels")
    $validLabels = Get-YoloLabelStats -LabelRoot (Join-Path $appRoot "data\valid\labels")
    $testLabels = Get-YoloLabelStats -LabelRoot (Join-Path $appRoot "data\test\labels")

    if ($CreateDataYaml) {
        $datasetPathInYaml = "."
        if ($UseAbsoluteYamlPath) {
            # YOLOv5 resolves `path: .` from its source folder in some invocation paths.
            # Absolute output keeps generated datasets trainable from external Python projects.
            $datasetPathInYaml = $appRoot.Replace("\", "/")
        }

        $trainPathInYaml = "data/train/images"
        $validPathInYaml = if ($validCopied -gt 0) { "data/valid/images" } else { "" }
        $testPathInYaml = if ($testCopied -gt 0) { "data/test/images" } else { "" }
        Write-DataYaml -Path $appDataYaml -DatasetPath $datasetPathInYaml -TrainImagePath $trainPathInYaml -ValidImagePath $validPathInYaml -TestImagePath $testPathInYaml -Names $effectiveClassNames
    }

    [pscustomobject]@{
        Dataset = $Dataset
        WorkspaceRoot = $workspaceRoot
        DatasetRoot = $datasetRoot
        RawRoot = $rawRoot
        AppTrainImages = $trainCopied
        AppValidImages = $validCopied
        AppTestImages = $testCopied
        AppTrainLabels = if ($null -ne $trainLabels) { $trainLabels.Written } else { 0 }
        AppValidLabels = if ($null -ne $validLabels) { $validLabels.Written } else { 0 }
        AppTestLabels = if ($null -ne $testLabels) { $testLabels.Written } else { 0 }
        AppDefectLabels = ((@($trainLabels, $validLabels, $testLabels) | Where-Object { $null -ne $_ } | Measure-Object -Property Defect -Sum).Sum)
        AppEmptyLabels = ((@($trainLabels, $validLabels, $testLabels) | Where-Object { $null -ne $_ } | Measure-Object -Property Empty -Sum).Sum)
        TrainPositiveOversampleFactor = $TrainPositiveOversampleFactor
        TrainPositiveOversampleAdded = $oversampleResult.Added
        TrainSplitRatio = $TrainSplitRatio
        TestSplitRatio = $TestSplitRatio
        ClassNames = $effectiveClassNames
        AppRoot = $appRoot
        TrainImageFolder = $appTrain
        ValidImageFolder = $appValid
        TestImageFolder = $appTest
    } | ConvertTo-Json | Write-Output
}
else {
    $preparedImages = Join-Path $preparedRoot "images"
    $preparedCopied = Copy-ImagesFlat -DestinationRoot $preparedImages -Images $images -SourceRoot $rawImagesDir
    [pscustomobject]@{
        Dataset = $Dataset
        WorkspaceRoot = $workspaceRoot
        DatasetRoot = $datasetRoot
        RawRoot = $rawRoot
        PreparedImages = $preparedImages
        ImageCount = $preparedCopied
    } | ConvertTo-Json | Write-Output
}

Write-Host "Done."
