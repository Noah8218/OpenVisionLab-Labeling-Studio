[CmdletBinding()]
param([string]$TestRoot = '')

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($TestRoot)) {
    $storage = if ([string]::IsNullOrWhiteSpace($env:OPENVISIONLAB_TEST_STORAGE_ROOT)) { [IO.Path]::GetTempPath() } else { $env:OPENVISIONLAB_TEST_STORAGE_ROOT }
    $TestRoot = Join-Path $storage ('launcher-path-tests-' + [Guid]::NewGuid().ToString('N'))
}
$TestRoot = [IO.Path]::GetFullPath($TestRoot)
if (Test-Path -LiteralPath $TestRoot) { throw "Test root already exists; refusing overwrite: $TestRoot" }
$fixture = Join-Path $TestRoot 'repository with spaces'
$scripts = Join-Path $fixture 'scripts'
$configDir = Join-Path $fixture 'config'
$otherWorkingDirectory = Join-Path $TestRoot 'other working directory'
New-Item -ItemType Directory -Path $scripts, $configDir, $otherWorkingDirectory -Force | Out-Null
$launcher = Join-Path $scripts 'start-labeling-workbench.ps1'
Copy-Item -LiteralPath (Join-Path $repoRoot 'scripts\start-labeling-workbench.ps1') -Destination $launcher
Copy-Item -LiteralPath (Join-Path $repoRoot 'config\labeling-runtime.example.json') -Destination $configDir
[xml]$props = Get-Content -LiteralPath (Join-Path $repoRoot 'Directory.Build.props') -Raw -Encoding UTF8
$version = [string]$props.Project.PropertyGroup.VersionPrefix
$propsPath = Join-Path $fixture 'Directory.Build.props'
Set-Content -LiteralPath $propsPath -Encoding UTF8 -Value "<Project><PropertyGroup><VersionPrefix>$version</VersionPrefix></PropertyGroup></Project>"
$examplePath = Join-Path $configDir 'labeling-runtime.example.json'
$localPath = Join-Path $configDir 'labeling-runtime.local.json'
$customPath = Join-Path $TestRoot 'explicit custom.json'
$cases = [System.Collections.Generic.List[string]]::new()

function Add-ExecutableFixture([string]$relativePath) {
    $path = Join-Path $fixture $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllBytes($path, [byte[]]@())
    return [IO.Path]::GetFullPath($path)
}
function Assert-Resolved([string]$name, [string]$expected, [hashtable]$arguments) {
    $actual = & $launcher @arguments -CheckOnly
    if ($actual -ne $expected) { throw "$name expected '$expected', got '$actual'" }
    $cases.Add($name)
}
function Assert-Rejected([string]$name, [string]$message, [hashtable]$arguments) {
    $caught = $false
    try { & $launcher @arguments -CheckOnly | Out-Null }
    catch { if ($_.Exception.Message -notlike "*$message*") { throw }; $caught = $true }
    if (-not $caught) { throw "$name unexpectedly succeeded" }
    $cases.Add($name)
}

# Empty files are path fixtures, not runnable applications. CheckOnly must never start them.
$debug = Add-ExecutableFixture 'artifacts\run\Debug\OpenVisionLab.LabelingStudio.exe'
$publish = Add-ExecutableFixture "artifacts\publish\Release\win-x64\$version\OpenVisionLab.LabelingStudio.exe"
$custom = Add-ExecutableFixture 'custom bundle\OpenVisionLab.LabelingStudio.exe'
$local = Add-ExecutableFixture 'local bundle\OpenVisionLab.LabelingStudio.exe'
$unrelated = Add-ExecutableFixture 'artifacts\publish\Release\win-x64\99.99.99\OpenVisionLab.LabelingStudio.exe'
$exampleHash = (Get-FileHash -LiteralPath $examplePath).Hash
Push-Location $otherWorkingDirectory
try {
    Assert-Resolved 'default Debug from another working directory' $debug @{}
    Assert-Resolved 'canonical publish version, not greatest folder name' $publish @{AppMode='Publish'}
    @{labelingApp=@{debugExecutable=$custom; publishExecutable=$custom}} | ConvertTo-Json | Set-Content -LiteralPath $customPath -Encoding UTF8
    @{labelingApp=@{debugExecutable=$local; publishExecutable=$local}} | ConvertTo-Json | Set-Content -LiteralPath $localPath -Encoding UTF8
    $localHash = (Get-FileHash -LiteralPath $localPath).Hash
    Assert-Resolved 'local configuration precedes example' $local @{AppMode='Publish'}
    Assert-Resolved 'explicit configuration precedes local' $custom @{AppMode='Publish';ConfigPath=$customPath}
    Assert-Resolved 'explicit configuration Debug' $custom @{AppMode='Debug';ConfigPath=$customPath}
    if ((Get-FileHash -LiteralPath $localPath).Hash -ne $localHash) { throw 'Local configuration was changed' }
    Remove-Item -LiteralPath $localPath
    Remove-Item -LiteralPath $publish
    Assert-Rejected 'missing current bundle does not fall back to old/other versions' 'Published app not found' @{AppMode='Publish'}
    Assert-Rejected 'preflight rejects runtime side effects' '-CheckOnly cannot be combined' @{StartYolo=$true}
    if ((Get-FileHash -LiteralPath $examplePath).Hash -ne $exampleHash) { throw 'Example configuration was changed' }
    $cases.Add('configuration files remain unchanged')
}
finally { Pop-Location }
Write-Output "LAUNCHER_PATH_TESTS=PASS Cases=$($cases.Count)"
$cases | ForEach-Object { Write-Output "PASS $_" }
