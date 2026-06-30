# Runs the ported Arcade SDK validation tests (src/Microsoft.DotNet.Arcade.Validation.Tests)
# against a specific set of locally produced Arcade/Helix SDK packages.
#
# This is the replacement for the dotnet/arcade-validation RepoTests flow: instead of flowing a
# new Arcade into arcade-validation and validating there, the arcade PR and official builds run
# these tests directly against the packages they just produced.
#
# The tests build small synthetic repositories using the Arcade SDK. To make them consume the
# *newly produced* SDK (rather than the bootstrap version pinned in global.json), this script
# discovers the produced Arcade.Sdk version and the local package feed(s) and passes them to the
# test host via environment variables (see RepoResources.Create).

[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = 'Release',
  # ';'-separated list of directories that contain the freshly built *.nupkg files
  # (e.g. artifacts/packages/<config>/Shipping and .../NonShipping). When omitted, defaults
  # to the standard Arcade artifacts package output directories for the given configuration.
  [string] $packageFeeds = '',
  # Explicit Arcade SDK version to validate. When omitted, it is derived from the
  # Microsoft.DotNet.Arcade.Sdk.*.nupkg found in the provided/default feeds.
  [string] $arcadeVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

. $PSScriptRoot\common\tools.ps1

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')

if ([string]::IsNullOrEmpty($packageFeeds)) {
  $shipping = Join-Path $repoRoot "artifacts/packages/$configuration/Shipping"
  $nonShipping = Join-Path $repoRoot "artifacts/packages/$configuration/NonShipping"
  $feedList = @($shipping, $nonShipping) | Where-Object { Test-Path $_ }
} else {
  $feedList = $packageFeeds.Split(';', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' }
}

if ($feedList.Count -eq 0) {
  Write-PipelineTelemetryError -Category 'ArcadeValidation' -Message "No local package feeds were found. Looked for: $packageFeeds"
  ExitWithExitCode 1
}

# Derive the produced Arcade SDK version from the nupkg if it wasn't supplied.
if ([string]::IsNullOrEmpty($arcadeVersion)) {
  foreach ($feed in $feedList) {
    $arcadePkg = Get-ChildItem -Path $feed -Filter 'Microsoft.DotNet.Arcade.Sdk.*.nupkg' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $arcadePkg) {
      if ($arcadePkg.Name -match '^Microsoft\.DotNet\.Arcade\.Sdk\.(.+)\.nupkg$') {
        $arcadeVersion = $Matches[1]
      }
      break
    }
  }
}

if ([string]::IsNullOrEmpty($arcadeVersion)) {
  Write-PipelineTelemetryError -Category 'ArcadeValidation' -Message "Could not determine the produced Arcade SDK version from feeds: $($feedList -join ';')"
  ExitWithExitCode 1
}

$feedsJoined = $feedList -join ';'
Write-Host "Validating newly produced Arcade SDK version '$arcadeVersion'."
Write-Host "Local package feeds: $feedsJoined"

$env:ARCADE_VALIDATION_SDK_VERSION = $arcadeVersion
$env:ARCADE_VALIDATION_LOCAL_FEEDS = $feedsJoined

$validationProject = Join-Path $repoRoot 'src/Microsoft.DotNet.Arcade.Validation.Tests/Microsoft.DotNet.Arcade.Validation.Tests.csproj'

try {
  & "$PSScriptRoot\common\build.ps1" `
    -configuration $configuration `
    -ci `
    -restore `
    -build `
    -test `
    -projects $validationProject `
    /bl:(Join-Path $repoRoot "artifacts/log/$configuration/ArcadeValidationTests.binlog")
  $exitCode = $LASTEXITCODE
}
finally {
  Remove-Item Env:\ARCADE_VALIDATION_SDK_VERSION -ErrorAction SilentlyContinue
  Remove-Item Env:\ARCADE_VALIDATION_LOCAL_FEEDS -ErrorAction SilentlyContinue
}

if ($exitCode -ne 0) {
  Write-PipelineTelemetryError -Category 'ArcadeValidation' -Message "Arcade SDK validation tests failed with exit code $exitCode."
  ExitWithExitCode $exitCode
}

ExitWithExitCode 0
