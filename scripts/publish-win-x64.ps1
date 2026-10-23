param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [string]$ReleaseVersion = "",

    [switch]$SelfContained,

    [switch]$FrameworkDependent,

    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"

if ($SelfContained.IsPresent -and $FrameworkDependent.IsPresent) {
    throw "-SelfContained and -FrameworkDependent cannot be used together."
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "OpenVisionLab.LabelingStudio.csproj"
$buildPropsPath = Join-Path $repoRoot "Directory.Build.props"
$publishRoot = Join-Path $repoRoot "artifacts\publish"
$selfContainedBuild = -not $FrameworkDependent.IsPresent

if ([string]::IsNullOrWhiteSpace($ReleaseVersion)) {
    [xml]$buildProps = Get-Content -LiteralPath $buildPropsPath -Raw
    $ReleaseVersion = [string]$buildProps.Project.PropertyGroup.VersionPrefix
}

if ($ReleaseVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "Release version must use numeric major.minor.patch form: '$ReleaseVersion'"
}

$assemblyVersion = "$ReleaseVersion.0"
$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot)
$resolvedPublishRoot = [System.IO.Path]::GetFullPath($publishRoot)
$publishDir = Join-Path $publishRoot "$Configuration\$Runtime\$ReleaseVersion"
$resolvedPublishDir = [System.IO.Path]::GetFullPath($publishDir)
$requiredPrefix = $resolvedPublishRoot.TrimEnd('\') + '\'

if (-not $resolvedPublishDir.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish path escapes the repository artifact root: $resolvedPublishDir"
}

$manifestPath = Join-Path $publishDir "release-manifest.json"
$textManifestPath = Join-Path $publishDir "publish-manifest.txt"
$manifestFileNames = @("release-manifest.json", "publish-manifest.txt")

function Get-NormalizedRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $resolvedBasePath = [System.IO.Path]::GetFullPath($BasePath).TrimEnd('\') + '\'
    $resolvedFullPath = [System.IO.Path]::GetFullPath($FullPath)
    if (-not $resolvedFullPath.StartsWith($resolvedBasePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Payload path escapes the release directory: $resolvedFullPath"
    }

    return $resolvedFullPath.Substring($resolvedBasePath.Length).Replace('\', '/')
}

function Get-Sha256Hash {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $stream = [System.IO.File]::OpenRead($Path)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

function Get-PayloadFiles {
    if (-not (Test-Path -LiteralPath $publishDir -PathType Container)) {
        throw "Release directory does not exist: $publishDir"
    }

    return @(
        Get-ChildItem -LiteralPath $publishDir -Recurse -File |
            Where-Object { $manifestFileNames -notcontains $_.Name } |
            Sort-Object { Get-NormalizedRelativePath -BasePath $publishDir -FullPath $_.FullName }
    )
}

function Assert-RequiredPayload {
    $requiredFiles = @(
        "OpenVisionLab.LabelingStudio.exe",
        "OpenVisionLab.LabelingStudio.dll",
        "log4net.config",
        "OpenVisionLab.Logging.dll",
        "OpenVisionLab.ImageCanvas.dll",
        "SharpGL.dll",
        "SharpGL.WinForms.dll",
        "LICENSE",
        "NOTICE",
        "THIRD-PARTY-NOTICES.txt",
        "scripts\compare-yolo-models.ps1"
    )

    if ($selfContainedBuild) {
        $requiredFiles += "hostfxr.dll"
    }

    foreach ($relativePath in $requiredFiles) {
        $requiredPath = Join-Path $publishDir $relativePath
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required release file is missing: $relativePath"
        }
    }
}

function Assert-NoForbiddenDevelopmentPaths {
    $forbiddenPatterns = @(
        "OpenVisionLab_Dev",
        "C:\Git\OpenVisionLab_Dev",
        "..\OpenVisionLab_Dev",
        "../OpenVisionLab_Dev"
    )

    $textExtensions = @(".config", ".json", ".txt", ".xml", ".ps1", ".cmd", ".bat")
    $textFiles = Get-ChildItem -LiteralPath $publishDir -Recurse -File |
        Where-Object {
            $textExtensions -contains $_.Extension -or
            $_.Name.EndsWith(".deps.json", [System.StringComparison]::OrdinalIgnoreCase) -or
            $_.Name.EndsWith(".runtimeconfig.json", [System.StringComparison]::OrdinalIgnoreCase)
        }

    foreach ($pattern in $forbiddenPatterns) {
        $matches = $textFiles | Select-String -SimpleMatch -Pattern $pattern -ErrorAction SilentlyContinue
        if ($matches) {
            $first = $matches | Select-Object -First 1
            throw "Release output contains forbidden DEV path '$pattern' in $($first.Path):$($first.LineNumber)"
        }
    }
}

function Assert-ReleasePackage {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Machine-readable release manifest is missing: $manifestPath"
    }

    if (-not (Test-Path -LiteralPath $textManifestPath -PathType Leaf)) {
        throw "Text release manifest is missing: $textManifestPath"
    }

    Assert-RequiredPayload
    Assert-NoForbiddenDevelopmentPaths

    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1) {
        throw "Unsupported release manifest schema: $($manifest.schemaVersion)"
    }

    if ($manifest.productVersion -ne $ReleaseVersion) {
        throw "Manifest product version mismatch: expected $ReleaseVersion, found $($manifest.productVersion)"
    }

    if ($manifest.assemblyVersion -ne $assemblyVersion) {
        throw "Manifest assembly version mismatch: expected $assemblyVersion, found $($manifest.assemblyVersion)"
    }

    if ($manifest.fileVersion -ne $assemblyVersion) {
        throw "Manifest file version mismatch: expected $assemblyVersion, found $($manifest.fileVersion)"
    }

    if ($manifest.build.configuration -ne $Configuration -or $manifest.build.runtimeIdentifier -ne $Runtime) {
        throw "Manifest build identity does not match the requested configuration/runtime."
    }

    if ([bool]$manifest.build.selfContained -ne $selfContainedBuild) {
        throw "Manifest self-contained identity does not match the requested package mode."
    }

    $applicationDll = Join-Path $publishDir "OpenVisionLab.LabelingStudio.dll"
    $actualAssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($applicationDll).Version.ToString()
    if ($actualAssemblyVersion -ne $assemblyVersion) {
        throw "Application assembly version mismatch: expected $assemblyVersion, found $actualAssemblyVersion"
    }

    $actualInformationalVersion =
        [System.Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).ProductVersion
    $actualFileVersion =
        [System.Diagnostics.FileVersionInfo]::GetVersionInfo($applicationDll).FileVersion
    if ($actualFileVersion -ne $manifest.fileVersion) {
        throw "Application file version does not match the release manifest."
    }

    if ($actualInformationalVersion -ne $manifest.informationalVersion) {
        throw "Application informational version does not match the release manifest."
    }

    $manifestEntries = @($manifest.files)
    $entryMap = @{}
    foreach ($entry in $manifestEntries) {
        $relativePath = [string]$entry.path
        if ([string]::IsNullOrWhiteSpace($relativePath) -or $relativePath.Contains('\')) {
            throw "Manifest path is empty or not normalized: '$relativePath'"
        }

        if ($entryMap.ContainsKey($relativePath)) {
            throw "Duplicate release manifest path: $relativePath"
        }

        $entryMap[$relativePath] = $entry
        $fullPath = Join-Path $publishDir $relativePath.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Manifest file is missing from the release: $relativePath"
        }

        $file = Get-Item -LiteralPath $fullPath
        if ($file.Length -ne [long]$entry.length) {
            throw "Release file length mismatch: $relativePath"
        }

        $actualHash = Get-Sha256Hash -Path $fullPath
        if ($actualHash -ne ([string]$entry.sha256).ToLowerInvariant()) {
            throw "Release file SHA256 mismatch: $relativePath"
        }
    }

    $actualPayloadPaths = @(
        Get-PayloadFiles |
            ForEach-Object { Get-NormalizedRelativePath -BasePath $publishDir -FullPath $_.FullName }
    )
    $unlistedPaths = @($actualPayloadPaths | Where-Object { -not $entryMap.ContainsKey($_) })
    $missingPaths = @($entryMap.Keys | Where-Object { $actualPayloadPaths -notcontains $_ })
    if ($unlistedPaths.Count -gt 0 -or $missingPaths.Count -gt 0) {
        throw "Release manifest payload set mismatch. Unlisted='$($unlistedPaths -join ',')' Missing='$($missingPaths -join ',')'"
    }
}

if ($VerifyOnly.IsPresent) {
    Assert-ReleasePackage
    Write-Host "Release package verification passed: $publishDir"
    exit 0
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$sourceCommit = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sourceCommit)) {
    throw "Unable to resolve the source commit."
}
$sourceDirty = -not [string]::IsNullOrWhiteSpace((& git -C $repoRoot status --porcelain --untracked-files=no | Out-String).Trim())
$shortCommit = $sourceCommit.Substring(0, [Math]::Min(12, $sourceCommit.Length))
$informationalVersion = "$ReleaseVersion+$shortCommit"
$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) {
    throw "Unable to resolve the .NET SDK version."
}

$selfContainedValue = $selfContainedBuild.ToString().ToLowerInvariant()
& dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    "--self-contained:$selfContainedValue" `
    -o $publishDir `
    -m:1 `
    /nr:false `
    /p:UseSharedCompilation=false `
    /p:ContinuousIntegrationBuild=true `
    "/p:VersionPrefix=$ReleaseVersion" `
    "/p:Version=$ReleaseVersion" `
    "/p:AssemblyVersion=$assemblyVersion" `
    "/p:FileVersion=$assemblyVersion" `
    "/p:InformationalVersion=$informationalVersion"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Assert-RequiredPayload
Assert-NoForbiddenDevelopmentPaths

$payloadEntries = @(
    Get-PayloadFiles |
        ForEach-Object {
            [ordered]@{
                path = Get-NormalizedRelativePath -BasePath $publishDir -FullPath $_.FullName
                length = $_.Length
                sha256 = Get-Sha256Hash -Path $_.FullName
            }
        }
)

$actualAssemblyVersion =
    [System.Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path $publishDir "OpenVisionLab.LabelingStudio.dll")
    ).Version.ToString()
$actualInformationalVersion =
    [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        (Join-Path $publishDir "OpenVisionLab.LabelingStudio.dll")
    ).ProductVersion
$actualFileVersion =
    [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
        (Join-Path $publishDir "OpenVisionLab.LabelingStudio.dll")
    ).FileVersion

if ($actualAssemblyVersion -ne $assemblyVersion) {
    throw "Published assembly version mismatch: expected $assemblyVersion, found $actualAssemblyVersion"
}
if ($actualFileVersion -ne $assemblyVersion) {
    throw "Published file version mismatch: expected $assemblyVersion, found $actualFileVersion"
}
if ($actualInformationalVersion -ne $informationalVersion) {
    throw "Published informational version mismatch: expected $informationalVersion, found $actualInformationalVersion"
}

$releaseManifest = [ordered]@{
    schemaVersion = 1
    productName = "OpenVisionLab Labeling Studio"
    productVersion = $ReleaseVersion
    assemblyVersion = $actualAssemblyVersion
    fileVersion = $actualFileVersion
    informationalVersion = $actualInformationalVersion
    source = [ordered]@{
        commit = $sourceCommit
        dirty = $sourceDirty
    }
    build = [ordered]@{
        sdkVersion = $sdkVersion
        configuration = $Configuration
        runtimeIdentifier = $Runtime
        selfContained = $selfContainedBuild
    }
    files = $payloadEntries
}

$releaseManifest |
    ConvertTo-Json -Depth 6 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

$payloadEntries |
    ForEach-Object { "{0}`t{1}`t{2}" -f $_.sha256, $_.length, $_.path } |
    Set-Content -LiteralPath $textManifestPath -Encoding UTF8

Assert-ReleasePackage

Write-Host "Published versioned release to $publishDir"
Write-Host "Release manifest: $manifestPath"
Write-Host "Release validation passed: version, identity, notices, payload set, and SHA256 hashes."
