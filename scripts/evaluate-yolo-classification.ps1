param(
    [string]$PythonExe = "C:\Git\yolov8\.venv\Scripts\python.exe",
    [string]$WorkerScript = "C:\Git\yolov8\labeling_tcp_client.py",
    [string]$BatchEvaluatorScript = '${repoRoot}\Runtime\Python\openvisionlab_yolo_classification_batch.py',
    [string]$ModelRoot = "C:\Git\yolov8",
    [string]$Weights = "",
    [string]$DatasetRoot = "",
    [ValidateSet("val", "test")]
    [string]$Split = "test",
    [int]$ImageSize = 64,
    [double]$Confidence = 0.0,
    [string]$OutputDirectory = "artifacts\yolo-classification-evaluation",
    [int]$MinimumTotalImageCount = 10,
    [int]$MinimumPerClassImageCount = 5,
    [double]$MinimumAccuracy = 0.9,
    [double]$MinimumPerClassAccuracy = 0.8,
    [double]$MinimumConfidence = 0.0,
    [switch]$UseLegacyPerImageWorker
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

function Resolve-PathValue([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $expanded = $Path.Replace('${repoRoot}', $repoRoot)
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

function Get-TextLinesSha256([string[]]$Lines) {
    $text = [string]::Join("`n", @($Lines))
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-ClassificationEvidenceFingerprint($NormalImages, $AbnormalImages) {
    $entries = @(
        @($NormalImages | ForEach-Object { "normal|$(Get-FileSha256 $_)" })
        @($AbnormalImages | ForEach-Object { "abnormal|$(Get-FileSha256 $_)" })
    ) | Sort-Object
    return Get-TextLinesSha256 $entries
}

function Test-SupportedImagePath([string]$Path) {
    $extension = [System.IO.Path]::GetExtension($Path)
    return @(".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff") -contains $extension.ToLowerInvariant()
}

function Get-ClassImages([string]$Root, [string]$SplitName, [string]$ClassName) {
    $directory = Join-Path (Join-Path $Root $SplitName) $ClassName
    if (-not (Test-Path -LiteralPath $directory -PathType Container)) {
        return @()
    }

    return @(Get-ChildItem -LiteralPath $directory -File -ErrorAction SilentlyContinue |
        Where-Object { Test-SupportedImagePath $_.FullName } |
        Sort-Object FullName |
        ForEach-Object { $_.FullName })
}

function Invoke-ClassificationSmoke([string]$ImagePath) {
    $arguments = @(
        $WorkerScript,
        "--smoke-test",
        "--model", "yolov8",
        "--weights", $Weights,
        "--image", $ImagePath,
        "--model-root", $ModelRoot,
        "--image-root", (Split-Path -Parent $ImagePath),
        "--device", "cpu",
        "--img-size", $ImageSize.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--conf", $Confidence.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    )

    $output = & $PythonExe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "YOLO classification smoke failed for $ImagePath. ExitCode=$LASTEXITCODE Output=$output"
    }

    $jsonLine = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1)
    if ($jsonLine.Count -eq 0) {
        throw "YOLO classification smoke returned no JSON for $ImagePath."
    }

    return $jsonLine[0] | ConvertFrom-Json
}

function Get-FirstClassificationCandidate($Result) {
    if ($null -eq $Result.candidates) {
        return $null
    }

    foreach ($candidate in $Result.candidates) {
        if ($candidate.imageLevel -eq $true -and $candidate.candidateType -eq "imageClassification") {
            return $candidate
        }
    }

    return @($Result.candidates | Select-Object -First 1)[0]
}

function New-Sample([string]$ImagePath, [string]$ExpectedClassName, [double]$MinimumConfidence) {
    $result = Invoke-ClassificationSmoke $ImagePath
    if ($result.ok -ne $true) {
        throw "YOLO classification smoke did not report ok=true for $ImagePath."
    }

    $candidate = Get-FirstClassificationCandidate $result
    if ($null -eq $candidate) {
        throw "YOLO classification smoke returned no candidate for $ImagePath."
    }

    $predictedClassName = if ($null -eq $candidate.className) { "" } else { [string]$candidate.className }
    $confidenceValue = 0.0
    if ($null -ne $candidate.confidence) {
        $confidenceValue = [double]$candidate.confidence
    }

    [pscustomobject]@{
        imagePath = $ImagePath
        expectedClassName = $ExpectedClassName
        predictedClassName = $predictedClassName
        confidence = $confidenceValue
        correct = $predictedClassName.Equals($ExpectedClassName, [System.StringComparison]::OrdinalIgnoreCase) -and $confidenceValue -ge $MinimumConfidence
    }
}

function Invoke-ClassificationBatch {
    $arguments = @(
        $BatchEvaluatorScript,
        "--worker-script", $WorkerScript,
        "--weights", $Weights,
        "--model-root", $ModelRoot,
        "--dataset-root", $DatasetRoot,
        "--split", $Split,
        "--device", "cpu",
        "--img-size", $ImageSize.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--conf", $Confidence.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    )

    $output = & $PythonExe @arguments 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "YOLO persistent-adapter batch evaluation failed. ExitCode=$exitCode Output=$output"
    }

    return @($output |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object {
            $result = $_ | ConvertFrom-Json
            $confidenceValue = [double]$result.confidence
            [pscustomobject]@{
                imagePath = [string]$result.imagePath
                expectedClassName = [string]$result.expectedClassName
                predictedClassName = [string]$result.predictedClassName
                confidence = $confidenceValue
                correct = ([string]$result.predictedClassName).Equals(
                    [string]$result.expectedClassName,
                    [System.StringComparison]::OrdinalIgnoreCase) -and
                    $confidenceValue -ge $MinimumConfidence
            }
        })
}

function Get-Count([object[]]$Items, [string]$ExpectedClassName) {
    return @($Items | Where-Object { $_.expectedClassName -ieq $ExpectedClassName }).Count
}

function Get-CorrectCount([object[]]$Items, [string]$ExpectedClassName) {
    return @($Items | Where-Object { $_.expectedClassName -ieq $ExpectedClassName -and $_.correct }).Count
}

function Get-LowConfidenceClassMatchCount([object[]]$Items, [double]$MinimumConfidence) {
    if ($MinimumConfidence -le 0.0) {
        return 0
    }

    return @($Items | Where-Object {
        $_.expectedClassName -ieq $_.predictedClassName -and [double]$_.confidence -lt $MinimumConfidence
    }).Count
}

function Get-Ratio([int]$Numerator, [int]$Denominator) {
    if ($Denominator -le 0) {
        return 0.0
    }

    return [Math]::Max(0.0, [Math]::Min(1.0, $Numerator / $Denominator))
}

function Get-ClampedRatio([double]$Value) {
    if ([double]::IsNaN($Value)) {
        return 0.0
    }

    return [Math]::Max(0.0, [Math]::Min(1.0, $Value))
}

function Format-Ratio([double]$Value) {
    return (Get-ClampedRatio $Value).ToString("0.###", [System.Globalization.CultureInfo]::InvariantCulture)
}

$PythonExe = Resolve-PathValue $PythonExe
$WorkerScript = Resolve-PathValue $WorkerScript
$BatchEvaluatorScript = Resolve-PathValue $BatchEvaluatorScript
$ModelRoot = Resolve-PathValue $ModelRoot
$Weights = Resolve-PathValue $Weights
$DatasetRoot = Resolve-PathValue $DatasetRoot
$OutputDirectory = Resolve-PathValue $OutputDirectory
$MinimumConfidence = Get-ClampedRatio $MinimumConfidence

Assert-File $PythonExe "Python executable"
Assert-File $WorkerScript "YOLO worker script"
if (-not $UseLegacyPerImageWorker) {
    Assert-File $BatchEvaluatorScript "YOLO classification batch evaluator"
}
Assert-Directory $ModelRoot "YOLO model root"
Assert-File $Weights "YOLO classification weights"
Assert-Directory $DatasetRoot "Classification dataset root"

$normalImages = @(Get-ClassImages $DatasetRoot $Split "normal")
$abnormalImages = @(Get-ClassImages $DatasetRoot $Split "abnormal")
$evaluationMode = if ($UseLegacyPerImageWorker) { "per-image-smoke-test" } else { "persistent-adapter-batch" }
$evaluationStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$samples = if ($UseLegacyPerImageWorker) {
    @(
        foreach ($imagePath in $normalImages) {
            New-Sample $imagePath "normal" $MinimumConfidence
        }
        foreach ($imagePath in $abnormalImages) {
            New-Sample $imagePath "abnormal" $MinimumConfidence
        }
    )
}
else {
    @(Invoke-ClassificationBatch)
}
$evaluationStopwatch.Stop()
$evaluationElapsedMs = [long][Math]::Round($evaluationStopwatch.Elapsed.TotalMilliseconds)
$expectedSampleCount = $normalImages.Count + $abnormalImages.Count
if ($samples.Count -ne $expectedSampleCount) {
    throw "YOLO classification evaluation returned $($samples.Count) samples; expected $expectedSampleCount."
}

$totalCount = $samples.Count
$normalCount = Get-Count $samples "normal"
$abnormalCount = Get-Count $samples "abnormal"
$normalCorrectCount = Get-CorrectCount $samples "normal"
$abnormalCorrectCount = Get-CorrectCount $samples "abnormal"
$correctCount = $normalCorrectCount + $abnormalCorrectCount
$lowConfidenceClassMatchCount = Get-LowConfidenceClassMatchCount $samples $MinimumConfidence
$accuracy = Get-Ratio $correctCount $totalCount
$normalAccuracy = Get-Ratio $normalCorrectCount $normalCount
$abnormalAccuracy = Get-Ratio $abnormalCorrectCount $abnormalCount
$holdReasons = @()

if ($totalCount -lt $MinimumTotalImageCount) {
    $holdReasons += "Evaluation uses $totalCount images; collect at least $MinimumTotalImageCount held-out images."
}

if ($normalCount -lt $MinimumPerClassImageCount) {
    $holdReasons += "Evaluation uses $normalCount normal images; collect at least $MinimumPerClassImageCount normal held-out images."
}

if ($abnormalCount -lt $MinimumPerClassImageCount) {
    $holdReasons += "Evaluation uses $abnormalCount abnormal images; collect at least $MinimumPerClassImageCount abnormal held-out images."
}

if ($accuracy -lt $MinimumAccuracy) {
    $holdReasons += "Accuracy $(Format-Ratio $accuracy) is below minimum $(Format-Ratio $MinimumAccuracy)."
}

if ($lowConfidenceClassMatchCount -gt 0) {
    $holdReasons += "$lowConfidenceClassMatchCount class-matching predictions were below minimum confidence $(Format-Ratio $MinimumConfidence)."
}

if ($normalAccuracy -lt $MinimumPerClassAccuracy) {
    $holdReasons += "Normal accuracy $(Format-Ratio $normalAccuracy) is below minimum $(Format-Ratio $MinimumPerClassAccuracy)."
}

if ($abnormalAccuracy -lt $MinimumPerClassAccuracy) {
    $holdReasons += "Abnormal accuracy $(Format-Ratio $abnormalAccuracy) is below minimum $(Format-Ratio $MinimumPerClassAccuracy)."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $OutputDirectory ("classification-evaluation-" + $timestamp)
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null
$summaryPath = Join-Path $runDirectory "classification-evaluation-summary.json"
$recommendation = if ($holdReasons.Count -eq 0) { "adopt" } else { "hold" }
$summary = [ordered]@{
    generatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    weightsPath = $Weights
    weightsSha256 = Get-FileSha256 $Weights
    datasetRoot = $DatasetRoot
    split = $Split
    evaluationMode = $evaluationMode
    evaluationElapsedMs = $evaluationElapsedMs
    averageEvaluationMsPerImage = if ($samples.Count -gt 0) { $evaluationStopwatch.Elapsed.TotalMilliseconds / $samples.Count } else { 0.0 }
    evidence = [ordered]@{
        fingerprintAlgorithm = "sha256-class-image-pairs-v1"
        fingerprintSha256 = Get-ClassificationEvidenceFingerprint $normalImages $abnormalImages
    }
    thresholds = [ordered]@{
        minimumTotalImageCount = $MinimumTotalImageCount
        minimumPerClassImageCount = $MinimumPerClassImageCount
        minimumAccuracy = $MinimumAccuracy
        minimumPerClassAccuracy = $MinimumPerClassAccuracy
        minimumConfidence = $MinimumConfidence
    }
    metrics = [ordered]@{
        totalImageCount = $totalCount
        normalImageCount = $normalCount
        abnormalImageCount = $abnormalCount
        correctImageCount = $correctCount
        normalCorrectCount = $normalCorrectCount
        abnormalCorrectCount = $abnormalCorrectCount
        lowConfidenceClassMatchCount = $lowConfidenceClassMatchCount
        accuracy = $accuracy
        normalAccuracy = $normalAccuracy
        abnormalAccuracy = $abnormalAccuracy
    }
    promotion = [ordered]@{
        recommendation = $recommendation
        reasons = $holdReasons
    }
    samples = $samples
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
Write-Output $summaryPath
