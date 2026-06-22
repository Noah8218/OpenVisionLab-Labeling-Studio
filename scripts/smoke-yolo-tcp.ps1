param(
    [string]$PythonExe = "",
    [string]$ClientScript = "",
    [string]$ModelRoot = "",
    [string]$WeightsPath = "",
    [string]$ImageRoot = "",
    [string]$ImagePath = "",
    [string]$Device = "cpu",
    [int]$ImgSize = 320,
    [double]$Confidence = 0.25,
    [double]$Iou = 0.45,
    [int]$Port = 0,
    [int]$TimeoutSeconds = 180,
    [int]$MinDetections = 1,
    [string]$OutputDirectory = "artifacts\python-smoke",
    [switch]$UseDetectImage,
    [int]$Repeat = 1
)

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Git\yolov5"
$defaultImageRoot = Join-Path $projectRoot "data\train\images"
if (-not (Test-Path -LiteralPath $defaultImageRoot -PathType Container)) {
    $defaultImageRoot = "C:\Git\py\KtemData"
}
if ([string]::IsNullOrWhiteSpace($PythonExe)) { $PythonExe = Join-Path $projectRoot ".venv\Scripts\python.exe" }
if ([string]::IsNullOrWhiteSpace($ClientScript)) { $ClientScript = Join-Path $projectRoot "labelling_tcp_client.py" }
if ([string]::IsNullOrWhiteSpace($ModelRoot)) { $ModelRoot = Join-Path $projectRoot "yolov5Master" }
if ([string]::IsNullOrWhiteSpace($WeightsPath)) { $WeightsPath = Join-Path $projectRoot "best.pt" }
if ([string]::IsNullOrWhiteSpace($ImageRoot)) { $ImageRoot = $defaultImageRoot }
if ($Repeat -lt 1) { $Repeat = 1 }
if ($Repeat -gt 1 -and -not $UseDetectImage) {
    throw "-Repeat currently requires -UseDetectImage so the smoke can send framed JSON requests on one worker connection."
}
if ([string]::IsNullOrWhiteSpace($ImagePath)) {
    $firstImage = Get-ChildItem -LiteralPath $ImageRoot -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".bmp", ".jpg", ".jpeg", ".png") } |
        Sort-Object Name |
        Select-Object -First 1
    $ImagePath = if ($null -ne $firstImage) { $firstImage.FullName } else { Join-Path $ImageRoot "Teaching_0.jpeg" }
}

function Assert-FileExists([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name not found: $Path"
    }
}

function Assert-DirectoryExists([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        throw "$Name not found: $Path"
    }
}

function Convert-ImageToPngBytes([string]$Path) {
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        $stream = New-Object System.IO.MemoryStream
        try {
            $image.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $image.Dispose()
    }
}

function Split-JsonObjectStream([string]$Text) {
    $objects = New-Object System.Collections.Generic.List[string]
    $depth = 0
    $start = -1
    $inString = $false
    $escaped = $false

    for ($i = 0; $i -lt $Text.Length; $i++) {
        $char = $Text[$i]

        if ($inString) {
            if ($escaped) {
                $escaped = $false
                continue
            }

            if ($char -eq [char]92) {
                $escaped = $true
                continue
            }

            if ($char -eq [char]34) {
                $inString = $false
            }

            continue
        }

        if ($char -eq [char]34) {
            $inString = $true
            continue
        }

        if ($char -eq [char]123) {
            if ($depth -eq 0) {
                $start = $i
            }

            $depth++
            continue
        }

        if ($char -eq [char]125) {
            if ($depth -gt 0) {
                $depth--
                if ($depth -eq 0 -and $start -ge 0) {
                    $objects.Add($Text.Substring($start, $i - $start + 1))
                    $start = -1
                }
            }
        }
    }

    return $objects
}

function Get-DetectionResultObjects([string]$Text) {
    $results = @()
    foreach ($json in (Split-JsonObjectStream $Text)) {
        try {
            $parsed = $json | ConvertFrom-Json
        }
        catch {
            continue
        }

        if ($parsed.type -in @("ResultDefect", "DetectImageResult")) {
            $results += $parsed
        }
    }

    return @($results)
}

function Quote-ProcessArgument([string]$Value) {
    if ($null -eq $Value) {
        return '""'
    }

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

function Join-ProcessArguments([string[]]$Values) {
    return (($Values | ForEach-Object { Quote-ProcessArgument $_ }) -join " ")
}

function Build-DetectImagePacket([string]$Path, [double]$Confidence) {
    $request = [ordered]@{
        type = "DetectImage"
        requestId = [guid]::NewGuid().ToString("N")
        imageId = [System.IO.Path]::GetFileNameWithoutExtension($Path)
        imagePath = $Path
        confidence = $Confidence
        model = "yolov5"
    }

    $json = $request | ConvertTo-Json -Compress
    return [System.Text.Encoding]::UTF8.GetBytes($json + "`n")
}

Assert-FileExists $PythonExe "Python executable"
Assert-FileExists $ClientScript "YOLO TCP client"
Assert-FileExists $WeightsPath "YOLO weights"
Assert-FileExists $ImagePath "Smoke image"
Assert-DirectoryExists $ModelRoot "YOLO model root"
Assert-DirectoryExists $ImageRoot "YOLO image root"

$resolvedOutputDirectory = Join-Path (Get-Location) $OutputDirectory
New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
$listener.Start()
$actualPort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$client = $null
$process = $null
$processKilled = $false

try {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $PythonExe
    $startInfo.WorkingDirectory = [System.IO.Path]::GetDirectoryName($ClientScript)
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $clientArguments = @(
        $ClientScript,
        "--host",
        "127.0.0.1",
        "--port",
        $actualPort.ToString(),
        "--timeout",
        $TimeoutSeconds.ToString(),
        "--weights",
        $WeightsPath,
        "--model-root",
        $ModelRoot,
        "--image-root",
        $ImageRoot,
        "--device",
        $Device,
        "--img-size",
        $ImgSize.ToString(),
        "--conf",
        $Confidence.ToString([System.Globalization.CultureInfo]::InvariantCulture),
        "--iou",
        $Iou.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    )
    if (-not ($UseDetectImage -and $Repeat -gt 1)) {
        $clientArguments += "--once"
    }

    $startInfo.Arguments = Join-ProcessArguments $clientArguments

    $process = [System.Diagnostics.Process]::Start($startInfo)
    $acceptTask = $listener.AcceptTcpClientAsync()
    if (-not $acceptTask.Wait([TimeSpan]::FromSeconds($TimeoutSeconds))) {
        throw "YOLO TCP client did not connect within $TimeoutSeconds seconds."
    }

    $client = $acceptTask.Result
    $stream = $client.GetStream()
    $stream.ReadTimeout = 1000
    $stream.WriteTimeout = 10000

    for ($requestIndex = 0; $requestIndex -lt $Repeat; $requestIndex++) {
        if ($UseDetectImage) {
            $packet = Build-DetectImagePacket $ImagePath $Confidence
        }
        else {
            $pngBytes = Convert-ImageToPngBytes $ImagePath
            $headerBytes = [System.Text.Encoding]::ASCII.GetBytes("StartDefect`n`n")
            $packet = New-Object byte[] ($headerBytes.Length + $pngBytes.Length)
            [System.Buffer]::BlockCopy($headerBytes, 0, $packet, 0, $headerBytes.Length)
            [System.Buffer]::BlockCopy($pngBytes, 0, $packet, $headerBytes.Length, $pngBytes.Length)
        }

        $stream.Write($packet, 0, $packet.Length)
        $stream.Flush()
    }

    $buffer = New-Object byte[] 65536
    $response = New-Object System.IO.MemoryStream
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while ([DateTime]::UtcNow -lt $deadline) {
            try {
                $read = $stream.Read($buffer, 0, $buffer.Length)
                if ($read -le 0) {
                    break
                }

                $response.Write($buffer, 0, $read)
                if ($UseDetectImage -and $Repeat -gt 1) {
                    $currentText = [System.Text.Encoding]::UTF8.GetString($response.ToArray())
                    if (@(Get-DetectionResultObjects $currentText).Count -ge $Repeat) {
                        break
                    }
                }
            }
            catch [System.IO.IOException] {
                if ($process.HasExited -and $response.Length -gt 0) {
                    break
                }
            }

            if ($process.HasExited -and $response.Length -gt 0) {
                break
            }
        }

        if ($UseDetectImage -and $Repeat -gt 1 -and $client -ne $null) {
            $client.Close()
            $client = $null
        }

        if (-not $process.WaitForExit(5000)) {
            $processKilled = $true
            $process.Kill()
            $process.WaitForExit()
        }

        $stdout = $process.StandardOutput.ReadToEnd()
        $stderr = $process.StandardError.ReadToEnd()
        $responseBytes = $response.ToArray()
    }
    finally {
        $response.Dispose()
    }

    $responseText = [System.Text.Encoding]::UTF8.GetString($responseBytes)
    $responsePath = Join-Path $resolvedOutputDirectory "yolo-tcp-response.txt"
    $stdoutPath = Join-Path $resolvedOutputDirectory "yolo-tcp-stdout.txt"
    $stderrPath = Join-Path $resolvedOutputDirectory "yolo-tcp-stderr.txt"
    Set-Content -LiteralPath $responsePath -Value $responseText -Encoding UTF8
    Set-Content -LiteralPath $stdoutPath -Value $stdout -Encoding UTF8
    Set-Content -LiteralPath $stderrPath -Value $stderr -Encoding UTF8

    if ($processKilled) {
        throw "YOLO TCP client did not exit after one detection. See $stderrPath"
    }

    if ($process.ExitCode -ne 0) {
        throw "YOLO TCP client exited with code $($process.ExitCode). See $stderrPath"
    }

    $results = @(Get-DetectionResultObjects $responseText)
    if ($results.Count -lt $Repeat) {
        throw "Detection result response was not received. See $responsePath"
    }

    $items = @()
    foreach ($result in $results) {
        if ($result.PSObject.Properties.Name -contains "error" -and -not [string]::IsNullOrWhiteSpace($result.error)) {
            throw "$($result.type) returned an error: $($result.error)"
        }

        if ($result.type -eq "DetectImageResult") {
            $items += @($result.candidates)
        }
        else {
            $items += @($result.items)
        }
    }

    $itemCount = @($items).Count
    if ($itemCount -lt $MinDetections) {
        throw "Expected at least $MinDetections detections, got $itemCount. See $responsePath"
    }

    $summary = [ordered]@{
        image = $ImagePath
        requestMode = if ($UseDetectImage) { "DetectImage" } else { "StartDefect" }
        requestCount = $Repeat
        resultCount = $results.Count
        port = $actualPort
        detectionCount = $itemCount
        firstClass = if ($itemCount -gt 0) { @($items)[0].className } else { "" }
        responsePath = $responsePath
        stdoutPath = $stdoutPath
        stderrPath = $stderrPath
    }

    $summaryPath = Join-Path $resolvedOutputDirectory "yolo-tcp-summary.json"
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    $summary | ConvertTo-Json -Depth 4
}
finally {
    if ($client -ne $null) {
        $client.Close()
    }

    $listener.Stop()

    if ($process -ne $null -and -not $process.HasExited) {
        $process.Kill()
    }
}
