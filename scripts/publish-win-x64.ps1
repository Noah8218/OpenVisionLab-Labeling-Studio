param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "MvcVisionSystem.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Configuration\$Runtime"

$resolvedRepoRoot = (Resolve-Path -LiteralPath $repoRoot).Path
$resolvedPublishParent = Split-Path -Parent $publishDir
if (-not (Test-Path -LiteralPath $resolvedPublishParent)) {
    New-Item -ItemType Directory -Path $resolvedPublishParent | Out-Null
}

$resolvedPublishParent = (Resolve-Path -LiteralPath $resolvedPublishParent).Path
if (-not $resolvedPublishParent.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish path escapes repository: $publishDir"
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

$selfContainedValue = $SelfContained.IsPresent.ToString().ToLowerInvariant()

dotnet build-server shutdown | Out-Host

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained:$selfContainedValue `
    -o $publishDir `
    -m:1 `
    /nodeReuse:false `
    -p:UseSharedCompilation=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$manifestPath = Join-Path $publishDir "publish-manifest.txt"
Get-ChildItem -LiteralPath $publishDir -Recurse -File |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = $_.FullName.Substring($publishDir.Length).TrimStart('\')
        "{0}`t{1}" -f $relativePath, $_.Length
    } |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

$forbiddenPatterns = @(
    "OpenVisionLab_Dev",
    "C:\Git\OpenVisionLab_Dev",
    "..\OpenVisionLab_Dev",
    "../OpenVisionLab_Dev"
)

$textExtensions = @(".config", ".deps.json", ".json", ".runtimeconfig.json", ".txt", ".xml", ".ps1", ".cmd", ".bat")
$textFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
    Where-Object {
        $extension = $_.Extension
        $name = $_.Name
        $textExtensions -contains $extension -or $name.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase)
    }

foreach ($pattern in $forbiddenPatterns) {
    $matches = $textFiles | Select-String -SimpleMatch -Pattern $pattern -ErrorAction SilentlyContinue
    if ($matches) {
        $first = $matches | Select-Object -First 1
        throw "Publish output contains forbidden DEV path '$pattern' in $($first.Path):$($first.LineNumber)"
    }
}

$requiredFiles = @(
    "MvcVisionSystem.exe",
    "MvcVisionSystem.dll",
    "log4net.config",
    "OpenVisionLab.Logging.dll",
    "OpenVisionLab.ImageCanvas.dll",
    "SharpGL.dll",
    "SharpGL.WinForms.dll"
)

foreach ($relativePath in $requiredFiles) {
    $requiredPath = Join-Path $publishDir $relativePath
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required publish file is missing: $relativePath"
    }
}

Write-Host "Published to $publishDir"
Write-Host "Manifest written to $manifestPath"
Write-Host "Publish validation passed: no DEV path references found."
