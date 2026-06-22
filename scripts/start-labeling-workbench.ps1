param(
    [string]$ConfigPath = "",
    [ValidateSet("Debug", "Publish")]
    [string]$AppMode = "Debug",
    [switch]$StartYolo,
    [switch]$StartPythonUi,
    [switch]$CheckYolo
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $localConfig = Join-Path $repoRoot "config\labeling-runtime.local.json"
    $exampleConfig = Join-Path $repoRoot "config\labeling-runtime.example.json"
    $ConfigPath = if (Test-Path -LiteralPath $localConfig -PathType Leaf) { $localConfig } else { $exampleConfig }
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Runtime config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

function Resolve-RepoPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $expandedPath = Expand-PathTokens $Path
    if ([System.IO.Path]::IsPathRooted($expandedPath)) {
        return [System.IO.Path]::GetFullPath($expandedPath)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $expandedPath))
}

function Expand-PathTokens([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    $repoParent = Split-Path -Parent $repoRoot
    $expanded = $Path.Replace('${repoRoot}', $repoRoot)
    $expanded = $expanded.Replace('${repoParent}', $repoParent)
    return $expanded
}

function Start-CheckedProcess([string]$FilePath, [string[]]$ArgumentList, [string]$WorkingDirectory) {
    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
        $command = Get-Command $FilePath -ErrorAction SilentlyContinue
        if ($null -eq $command) {
            throw "Executable not found: $FilePath"
        }

        $FilePath = $command.Source
    }

    $startInfo = @{
        FilePath = $FilePath
        WorkingDirectory = if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) { Split-Path -Parent $FilePath } else { $WorkingDirectory }
    }

    if ($ArgumentList -and $ArgumentList.Count -gt 0) {
        $startInfo.ArgumentList = $ArgumentList
    }

    Start-Process @startInfo
}

function Resolve-FirstImage([string]$ImageRoot) {
    if ([string]::IsNullOrWhiteSpace($ImageRoot) -or -not (Test-Path -LiteralPath $ImageRoot -PathType Container)) {
        return ""
    }

    $image = Get-ChildItem -LiteralPath $ImageRoot -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".bmp", ".jpg", ".jpeg", ".png") } |
        Sort-Object Name |
        Select-Object -First 1

    if ($null -eq $image) {
        return ""
    }

    return $image.FullName
}

function Resolve-PythonExecutable {
    $python = $config.python
    $projectRoot = Resolve-RepoPath $python.projectRoot
    $configured = Resolve-RepoPath $python.pythonExecutable
    if (-not [string]::IsNullOrWhiteSpace($configured) -and (Test-Path -LiteralPath $configured -PathType Leaf)) {
        return $configured
    }

    $venvPython = Join-Path $projectRoot ".venv\Scripts\python.exe"
    if (Test-Path -LiteralPath $venvPython -PathType Leaf) {
        return $venvPython
    }

    $command = Get-Command "python.exe" -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    throw "Python executable was not found. Install the YOLO venv or set python.pythonExecutable in $ConfigPath."
}

function Invoke-YoloSmokeCheck {
    $python = $config.python
    $projectRoot = Resolve-RepoPath $python.projectRoot
    $pythonExe = Resolve-PythonExecutable
    $clientScript = Resolve-RepoPath $python.clientScript
    $modelRoot = Join-Path $projectRoot "yolov5Master"
    $weightsPath = Resolve-RepoPath $python.weights
    $imageRoot = Resolve-RepoPath $python.imageRoot
    $imageSize = if ($null -ne $python.imageSize) { [string]$python.imageSize } else { "320" }
    $imagePath = Resolve-FirstImage $imageRoot

    if (-not (Test-Path -LiteralPath $clientScript -PathType Leaf)) {
        throw "YOLO client script not found: $clientScript"
    }
    if (-not (Test-Path -LiteralPath $modelRoot -PathType Container)) {
        throw "YOLO model root not found: $modelRoot"
    }
    if (-not (Test-Path -LiteralPath $weightsPath -PathType Leaf)) {
        throw "YOLO weights not found: $weightsPath"
    }
    if ([string]::IsNullOrWhiteSpace($imagePath)) {
        throw "Sample image not found under: $imageRoot"
    }

    Write-Host "YOLO smoke check image: $imagePath"
    & $pythonExe $clientScript --smoke-test --weights $weightsPath --model-root $modelRoot --image $imagePath --img-size $imageSize --conf ([string]$python.confidence)
    if ($LASTEXITCODE -ne 0) {
        throw "YOLO smoke check failed with exit code $LASTEXITCODE"
    }
}

function Start-YoloRuntime {
    $python = $config.python
    $launcherExe = Resolve-RepoPath $python.launcherExe
    $launcherScript = Resolve-RepoPath $python.launcherScript
    $projectRoot = Resolve-RepoPath $python.projectRoot
    $pythonExe = Resolve-PythonExecutable
    $clientScript = Resolve-RepoPath $python.clientScript
    $weightsPath = Resolve-RepoPath $python.weights
    $imageRoot = Resolve-RepoPath $python.imageRoot
    $modelRoot = Join-Path $projectRoot "yolov5Master"
    $imageSize = if ($null -ne $python.imageSize) { [string]$python.imageSize } else { "320" }

    if (Test-Path -LiteralPath $launcherExe -PathType Leaf) {
        $launcherArguments = @(
            "--project-root",
            $projectRoot,
            "--python",
            $pythonExe,
            "--script",
            $clientScript,
            "--weights",
            $weightsPath,
            "--model-root",
            $modelRoot,
            "--image-root",
            $imageRoot,
            "--conf",
            [string]$python.confidence,
            "--img-size",
            $imageSize,
            "--preload"
        )

        if (-not [string]::IsNullOrWhiteSpace([string]$python.device)) {
            $launcherArguments += @("--device", [string]$python.device)
        }

        Start-CheckedProcess $launcherExe ([string[]]$launcherArguments) $projectRoot
        return
    }

    if (-not (Test-Path -LiteralPath $launcherScript -PathType Leaf)) {
        throw "YOLO launcher was not found. Expected exe '$launcherExe' or script '$launcherScript'."
    }

    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        $launcherScript,
        "-HostAddress",
        [string]$python.host,
        "-Port",
        [string]$python.port,
        "-Confidence",
        [string]$python.confidence,
        "-ImageSize",
        $imageSize,
        "-Preload",
        "-Device",
        [string]$python.device
    )

    Start-CheckedProcess "powershell.exe" $arguments $projectRoot
}

function Start-PythonUiRuntime {
    $python = $config.python
    $uiScript = Resolve-RepoPath $python.uiScript
    $projectRoot = Resolve-RepoPath $python.projectRoot
    if (-not (Test-Path -LiteralPath $uiScript -PathType Leaf)) {
        throw "Python UI launcher was not found: $uiScript"
    }

    Start-CheckedProcess "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $uiScript) $projectRoot
}

$appPath = if ($AppMode -eq "Publish") {
    Resolve-RepoPath $config.labelingApp.publishExecutable
}
else {
    Resolve-RepoPath $config.labelingApp.debugExecutable
}

if ($StartYolo) {
    Start-YoloRuntime
}

if ($StartPythonUi) {
    Start-PythonUiRuntime
}

if ($CheckYolo) {
    Invoke-YoloSmokeCheck
}

Start-CheckedProcess $appPath ([string[]]@()) (Split-Path -Parent $appPath)

Write-Host "Labeling app started: $appPath"
if ($StartYolo) {
    Write-Host "YOLO runtime start requested."
}
if ($StartPythonUi) {
    Write-Host "Python UI start requested."
}
if ($CheckYolo) {
    Write-Host "YOLO smoke check completed."
}
