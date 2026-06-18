param(
    [string]$ConfigPath = "",
    [ValidateSet("Debug", "Publish")]
    [string]$AppMode = "Debug",
    [switch]$StartYolo,
    [switch]$StartPythonUi
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

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
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

function Start-YoloRuntime {
    $python = $config.python
    $launcherExe = Resolve-RepoPath $python.launcherExe
    $launcherScript = Resolve-RepoPath $python.launcherScript
    $projectRoot = Resolve-RepoPath $python.projectRoot

    if (Test-Path -LiteralPath $launcherExe -PathType Leaf) {
        Start-CheckedProcess $launcherExe ([string[]]@()) $projectRoot
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

Start-CheckedProcess $appPath ([string[]]@()) (Split-Path -Parent $appPath)

Write-Host "Labeling app started: $appPath"
if ($StartYolo) {
    Write-Host "YOLO runtime start requested."
}
if ($StartPythonUi) {
    Write-Host "Python UI start requested."
}
