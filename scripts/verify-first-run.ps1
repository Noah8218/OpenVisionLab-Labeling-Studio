param(
    [string]$Configuration = "Debug",
    [string]$ConfigPath = "",
    [switch]$SkipBuild,
    [switch]$SkipTests,
    [switch]$SkipYoloSmoke,
    [switch]$SkipScriptSyntax,
    [switch]$RunWpfSmoke,
    [switch]$RunPublishWpfSmoke
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $localConfig = Join-Path $repoRoot "config\labeling-runtime.local.json"
    $exampleConfig = Join-Path $repoRoot "config\labeling-runtime.example.json"
    $ConfigPath = if (Test-Path -LiteralPath $localConfig -PathType Leaf) { $localConfig } else { $exampleConfig }
}

function Write-Step([string]$Name) {
    Write-Host ""
    Write-Host "== $Name =="
}

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

function Test-ScriptSyntax([string]$Path) {
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors) | Out-Null
    if ($errors.Count -gt 0) {
        $messages = $errors | ForEach-Object { $_.Message }
        throw "PowerShell syntax error in $Path`r`n$($messages -join "`r`n")"
    }
}

function Resolve-FirstImage([string]$ImageRoot) {
    if ([string]::IsNullOrWhiteSpace($ImageRoot) -or -not (Test-Path -LiteralPath $ImageRoot -PathType Container)) {
        return ""
    }

    $image = Get-ChildItem -LiteralPath $ImageRoot -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in @(".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff") } |
        Sort-Object Name |
        Select-Object -First 1

    if ($null -eq $image) {
        return ""
    }

    return $image.FullName
}

function Resolve-PythonExecutableFromConfig($Config) {
    $projectRoot = Resolve-RepoPath $Config.python.projectRoot
    $configured = Resolve-RepoPath $Config.python.pythonExecutable
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

    return ""
}

function Invoke-RuntimeConfigCheck {
    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw "Runtime config not found: $ConfigPath"
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $projectRoot = Resolve-RepoPath $config.python.projectRoot
    $clientScript = Resolve-RepoPath $config.python.clientScript
    $weightsPath = Resolve-RepoPath $config.python.weights
    $imageRoot = Resolve-RepoPath $config.python.imageRoot
    $pythonExe = Resolve-PythonExecutableFromConfig $config
    $sampleImage = Resolve-FirstImage $imageRoot

    if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
        throw "YOLO project root not found: $projectRoot"
    }
    if (-not (Test-Path -LiteralPath $clientScript -PathType Leaf)) {
        throw "YOLO client script not found: $clientScript"
    }
    if (-not (Test-Path -LiteralPath $weightsPath -PathType Leaf)) {
        throw "YOLO weights not found: $weightsPath"
    }
    if (-not (Test-Path -LiteralPath $imageRoot -PathType Container)) {
        throw "YOLO image root not found: $imageRoot"
    }
    if ([string]::IsNullOrWhiteSpace($sampleImage)) {
        throw "Sample image not found under: $imageRoot"
    }
    if ([string]::IsNullOrWhiteSpace($pythonExe)) {
        throw "Python executable was not found. Install the YOLO venv or update $ConfigPath."
    }

    Write-Host "[OK] Runtime config: $ConfigPath"
    Write-Host "[OK] Python: $pythonExe"
    Write-Host "[OK] YOLO project: $projectRoot"
    Write-Host "[OK] Weights: $weightsPath"
    Write-Host "[OK] Sample image: $sampleImage"
}

function Invoke-YoloSmoke {
    if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
        throw "Runtime config not found: $ConfigPath"
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    $projectRoot = Resolve-RepoPath $config.python.projectRoot
    $smokeLauncher = Join-Path $projectRoot "launchers\smoke-test-yolo-worker.bat"

    if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
        throw "YOLO project root not found: $projectRoot"
    }

    if (-not (Test-Path -LiteralPath $smokeLauncher -PathType Leaf)) {
        throw "YOLO smoke launcher not found: $smokeLauncher"
    }

    Push-Location $projectRoot
    try {
        & $smokeLauncher
        if ($LASTEXITCODE -ne 0) {
            throw "YOLO smoke launcher failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-WpfSmoke([string]$ExePath, [string]$Label) {
    $exePath = Resolve-RepoPath $ExePath
    if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
        throw "$Label executable not found: $exePath"
    }

    $process = Start-Process -FilePath $exePath -WorkingDirectory (Split-Path -Parent $exePath) -PassThru
    try {
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline -and -not $process.HasExited) {
            $process.Refresh()
            if ($process.MainWindowHandle -ne 0) {
                Write-Host "[OK] $Label opened: $($process.MainWindowTitle)"
                return
            }

            Start-Sleep -Milliseconds 500
        }

        if ($process.HasExited) {
            throw "$Label exited early with code $($process.ExitCode)"
        }

        throw "$Label did not create a main window within 30 seconds."
    }
    finally {
        if ($process -and -not $process.HasExited) {
            $null = $process.CloseMainWindow()
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
                $process.WaitForExit()
            }
        }
    }
}

Push-Location $repoRoot
try {
    if (-not $SkipScriptSyntax) {
        Write-Step "PowerShell scripts"
        foreach ($script in @(
            "scripts\start-labeling-workbench.ps1",
            "scripts\smoke-yolo-tcp.ps1",
            "scripts\smoke-yolo-lifecycle.ps1",
            "scripts\smoke-yolo-workflow.ps1",
            "scripts\compare-yolo-models.ps1",
            "scripts\verify-wpf-roi-object-interactions.ps1",
            "scripts\verify-wpf-segmentation-object-interactions.ps1",
            "scripts\verify-wpf-annotation-object-interactions.ps1",
            "scripts\verify-wpf-annotation-objects.ps1",
            "scripts\verify-first-run.ps1"
        )) {
            $path = Join-Path $repoRoot $script
            Test-ScriptSyntax $path
            Write-Host "[OK] $script"
        }
    }

    Write-Step "Runtime config"
    Invoke-RuntimeConfigCheck

    if (-not $SkipBuild) {
        Write-Step "Build"
        if ($SkipTests) {
            dotnet build ".\OpenVisionLab.LabelingStudio.csproj" -c $Configuration -p:Platform=x64 -v:minimal -m
        }
        else {
            dotnet build ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c $Configuration -v:minimal -m
        }
    }

    if (-not $SkipTests) {
        Write-Step "Tests"
        dotnet run --project ".\tests\LabelingApplication.Tests\LabelingApplication.Tests.csproj" -c $Configuration --no-build
    }

    if (-not $SkipYoloSmoke) {
        Write-Step "YOLO smoke"
        Invoke-YoloSmoke
    }

    if ($RunWpfSmoke) {
        Write-Step "WPF smoke"
        Invoke-WpfSmoke (Join-Path $repoRoot "artifacts\run\$Configuration\OpenVisionLab.LabelingStudio.exe") "WPF shell"
    }

    if ($RunPublishWpfSmoke) {
        Write-Step "Publish WPF smoke"
        $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
        Invoke-WpfSmoke $config.labelingApp.publishExecutable "Publish WPF shell"
    }

    Write-Host ""
    Write-Host "First-run verification passed."
}
finally {
    Pop-Location
}
