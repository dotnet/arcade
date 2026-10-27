param(
  [Parameter(Mandatory=$true)][string]$DotNetRoot,
  [Parameter(Mandatory=$true)][string]$RepositorySdkVersion,
  [Parameter(Mandatory=$true)][string]$ToolsSdkVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$sdkSource = $DotNetRoot
foreach ($version in @($RepositorySdkVersion, $ToolsSdkVersion)) {
  if (!(Test-Path "$DotNetRoot\sdk\$version")) {
    throw "SDK '$version' must already be installed in '$DotNetRoot'."
  }
}

$disableConfigureToolsetImport = $true
. "$PSScriptRoot\..\common\tools.ps1"
$root = Join-Path ([IO.Path]::GetTempPath()) ("arcade-sdk-tests-" + [guid]::NewGuid().ToString('N'))
$links = [Collections.Generic.List[string]]::new()
$installs = [Collections.Generic.List[string]]::new()

function Add-Sdk([string]$destination, [string]$version) {
  New-Item -ItemType Directory "$destination\sdk" -Force | Out-Null
  if (!(Test-Path "$destination\dotnet.exe")) {
    Copy-Item "$sdkSource\dotnet.exe" "$destination\dotnet.exe"
    foreach ($directory in @('host', 'shared')) {
      $link = Join-Path $destination $directory
      New-Item -ItemType Junction $link -Value (Join-Path $sdkSource $directory) | Out-Null
      $links.Add($link)
    }
  }
  $link = Join-Path $destination "sdk\$version"
  New-Item -ItemType Junction $link -Value "$sdkSource\sdk\$version" | Out-Null
  $links.Add($link)
}

# Stage existing SDK files rather than downloading them; SDK resolution uses the real host.
function InstallDotNetSdk([string]$dotnetRoot, [string]$version) {
  $installs.Add($version)
  Add-Sdk $dotnetRoot $version
}

function InstallDotNet([string]$dotnetRoot, [string]$version, [bool]$skipNonVersionedFiles,
                       [string]$runtimeSourceFeed, [string]$runtimeSourceFeedKey, [switch]$noPath) {
  if (!$skipNonVersionedFiles) { throw 'The additional SDK must preserve the engineering host.' }
  InstallDotNetSdk $dotnetRoot $version
}

$scenarios = @(
  'matching', 'no-sdk', 'missing', 'both', 'major', 'latestMajor',
  'no-install', 'path', 'global-fallback', 'runtimes', 'cached', 'local-major', 'both-latestMajor'
)
try {
  foreach ($scenario in $scenarios) {
    $RepoRoot = Join-Path $root $scenario
    $external = Join-Path $RepoRoot 'external'
    $fallback = Join-Path $RepoRoot '.dotnet'
    $repoVersion = if ($scenario -eq 'matching') { $ToolsSdkVersion } else { $RepositorySdkVersion }
    $json = @{
      sdk = @{ version = $repoVersion; allowPrerelease = $true }
      tools = @{ dotnet = $ToolsSdkVersion }
    }
    if ($scenario -eq 'no-sdk') { $json.Remove('sdk') }
    if ($scenario -in @('major', 'latestMajor')) { $json.sdk.rollForward = $scenario }
    if ($scenario -eq 'local-major') { $json.sdk.rollForward = 'major' }
    if ($scenario -eq 'both-latestMajor') { $json.sdk.rollForward = 'latestMajor' }
    Add-Sdk $external $ToolsSdkVersion
    if ($scenario -in @('both', 'both-latestMajor')) { Add-Sdk $external $RepositorySdkVersion }
    if ($scenario -eq 'local-major') { Add-Sdk $fallback $ToolsSdkVersion }
    $json | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $RepoRoot 'global.json')
    $GlobalJson = Get-Content (Join-Path $RepoRoot 'global.json') -Raw | ConvertFrom-Json
    $globalJsonHasRuntimes = $scenario -eq 'runtimes'
    $useInstalledDotNetCli = $scenario -ne 'local-major'
    $env:PATH = "$external;$env:PATH"
    $env:DOTNET_INSTALL_DIR = if ($scenario -in @('path', 'local-major')) { $null } else { $external }
    $env:DOTNET_GLOBAL_INSTALL_DIR = if ($scenario -eq 'global-fallback') { $fallback } else { $null }
    Remove-Variable _DotNetInstallDir -Scope Global -ErrorAction SilentlyContinue
    $installs.Clear()
    $install = $scenario -ne 'no-install'
    $reuse = $scenario -in @('matching', 'no-sdk', 'both', 'major', 'latestMajor', 'both-latestMajor', 'no-install')
    $noInstalls = $reuse -or $scenario -eq 'local-major'
    $expectedRoot = if ($reuse) { $external } else { $fallback }

    $actualRoot = InitializeDotNetCli -install:$install
    if ($actualRoot -ne $expectedRoot) { throw "$scenario selected '$actualRoot', expected '$expectedRoot'." }
    if ($noInstalls -and $installs.Count -ne 0) { throw "$scenario unnecessarily installed an SDK." }
    if (!$noInstalls -and ($installs -join ',') -ne "$ToolsSdkVersion,$RepositorySdkVersion") {
      throw "$scenario installed unexpected SDKs: $installs"
    }
    if ($scenario -eq 'cached') {
      $installs.Clear()
      if ((InitializeDotNetCli -install:$true) -ne $actualRoot -or $installs.Count -ne 0) {
        throw 'Cached initialization changed the root or installed another SDK.'
      }
    }
    if ($install) {
      Push-Location $RepoRoot
      try {
        $resolved = & "$actualRoot\dotnet.exe" --version
        $expectedVersion = if ($noInstalls -and $scenario -ne 'both') { $ToolsSdkVersion } else { $RepositorySdkVersion }
        if ($LASTEXITCODE -ne 0 -or $resolved -ne $expectedVersion) {
          throw "$scenario resolved '$resolved', expected '$expectedVersion'."
        }
      }
      finally {
        Pop-Location
      }
    }
    Write-Host "PASS: $scenario"
  }
}
finally {
  foreach ($link in $links) { [IO.Directory]::Delete($link) }
  if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
