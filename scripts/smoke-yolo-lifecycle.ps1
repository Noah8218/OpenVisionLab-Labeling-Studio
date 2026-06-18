param(
    [string]$PythonExe = "",
    [string]$ClientScript = "",
    [string]$ModelRoot = "",
    [string]$WeightsPath = "",
    [string]$ImageRoot = "",
    [string]$Device = "cpu",
    [double]$Confidence = 0.25,
    [int]$Iterations = 3,
    [int]$ConnectTimeoutSeconds = 90,
    [int]$StopTimeoutSeconds = 10,
    [string]$OutputDirectory = "artifacts\python-smoke"
)

$ErrorActionPreference = "Stop"

$projectRoot = "C:\Git\yolov5"
$defaultImageRoot = "C:\Git\py\KtemData"
if ([string]::IsNullOrWhiteSpace($PythonExe)) { $PythonExe = Join-Path $projectRoot ".venv\Scripts\python.exe" }
if ([string]::IsNullOrWhiteSpace($ClientScript)) { $ClientScript = Join-Path $projectRoot "labelling_tcp_client.py" }
if ([string]::IsNullOrWhiteSpace($ModelRoot)) { $ModelRoot = Join-Path $projectRoot "yolov5Master" }
if ([string]::IsNullOrWhiteSpace($WeightsPath)) { $WeightsPath = Join-Path $projectRoot "best.pt" }
if ([string]::IsNullOrWhiteSpace($ImageRoot)) { $ImageRoot = $defaultImageRoot }

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

function Stop-ProcessTree([System.Diagnostics.Process]$Process) {
    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    try {
        $Process.Kill($true)
    }
    catch [System.Management.Automation.MethodException] {
        $Process.Kill()
    }
}

Assert-FileExists $PythonExe "Python executable"
Assert-FileExists $ClientScript "YOLO TCP client"
Assert-FileExists $WeightsPath "YOLO weights"
Assert-DirectoryExists $ModelRoot "YOLO model root"
Assert-DirectoryExists $ImageRoot "YOLO image root"

if ($Iterations -lt 1) {
    throw "Iterations must be at least 1."
}

$resolvedOutputDirectory = Join-Path (Get-Location) $OutputDirectory
New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$results = @()

for ($index = 1; $index -le $Iterations; $index++) {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $actualPort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    $client = $null
    $process = $null
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    try {
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $PythonExe
        $startInfo.WorkingDirectory = [System.IO.Path]::GetDirectoryName($ClientScript)
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.Arguments = Join-ProcessArguments @(
            $ClientScript,
            "--host",
            "127.0.0.1",
            "--port",
            $actualPort.ToString(),
            "--timeout",
            $ConnectTimeoutSeconds.ToString(),
            "--weights",
            $WeightsPath,
            "--model-root",
            $ModelRoot,
            "--image-root",
            $ImageRoot,
            "--device",
            $Device,
            "--conf",
            $Confidence.ToString([System.Globalization.CultureInfo]::InvariantCulture),
            "--retry"
        )

        $process = [System.Diagnostics.Process]::Start($startInfo)
        $acceptTask = $listener.AcceptTcpClientAsync()
        if (-not $acceptTask.Wait([TimeSpan]::FromSeconds($ConnectTimeoutSeconds))) {
            throw "Iteration ${index}: YOLO TCP client did not connect within $ConnectTimeoutSeconds seconds."
        }

        $client = $acceptTask.Result
        $connectedMs = $stopwatch.ElapsedMilliseconds

        $stopwatch.Restart()
        if (-not $process.HasExited) {
            Stop-ProcessTree $process
        }

        if (-not $process.WaitForExit($StopTimeoutSeconds * 1000)) {
            throw "Iteration ${index}: YOLO TCP client did not stop within $StopTimeoutSeconds seconds."
        }

        $stoppedMs = $stopwatch.ElapsedMilliseconds
        $stdoutPath = Join-Path $resolvedOutputDirectory ("yolo-lifecycle-{0}-stdout.txt" -f $index)
        $stderrPath = Join-Path $resolvedOutputDirectory ("yolo-lifecycle-{0}-stderr.txt" -f $index)
        Set-Content -LiteralPath $stdoutPath -Value $process.StandardOutput.ReadToEnd() -Encoding UTF8
        Set-Content -LiteralPath $stderrPath -Value $process.StandardError.ReadToEnd() -Encoding UTF8

        $results += [ordered]@{
            iteration = $index
            port = $actualPort
            connectedMs = $connectedMs
            stoppedMs = $stoppedMs
            stdoutPath = $stdoutPath
            stderrPath = $stderrPath
        }
    }
    finally {
        if ($client -ne $null) {
            $client.Close()
        }

        $listener.Stop()

        if ($process -ne $null -and -not $process.HasExited) {
            Stop-ProcessTree $process
            $process.WaitForExit()
        }
    }
}

$summaryPath = Join-Path $resolvedOutputDirectory "yolo-lifecycle-summary.json"
$summary = [ordered]@{
    iterations = $Iterations
    results = $results
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 5
