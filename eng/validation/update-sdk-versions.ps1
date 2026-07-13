# Self-build validation helper: bump the repo to the newly built Arcade/Helix SDK using the canonical
# darc flow, so the subsequent `build` validates the SDK this build produced (not the bootstrap SDK).
#
# arcade tracks Microsoft.DotNet.Arcade.Sdk / Microsoft.DotNet.Helix.Sdk in eng/Version.Details.xml,
# so `darc update-dependencies --packages-folder` reads the produced versions from the artifacts folder
# and updates Version.Details.xml, eng/Versions.props, and global.json's msbuild-sdks (and refreshes
# eng/common from the built Arcade). The local NuGet feed itself is added separately (see
# use-built-sdk.ps1 -AddFeedOnly), since darc does not touch NuGet.config.
#
# Windows-only (uses darc via Get-Darc); the signing validation, which runs cross-platform, uses the
# pure-file-edit path in use-built-sdk.ps1 instead.

Param(
  [Parameter(Mandatory=$true)][string] $PackagesSource  # Folder containing the freshly built *.nupkg (the downloaded build artifacts).
)

# Set CI defaults and skip the toolset-configuration import before dot-sourcing tools.ps1 (that import
# runs eng/configure-toolset.ps1, which ends with `exit` and could terminate this script).
$ci = $true
$disableConfigureToolsetImport = $true
. $PSScriptRoot\..\common\tools.ps1

$ErrorActionPreference = 'Stop'

$darc = Get-Darc

# darc's --packages-folder scan is NON-recursive (Directory.GetFiles(path, "*.nupkg")), but the build
# artifacts nest the packages under packages/<config>/{Shipping,NonShipping}. Gather the produced
# Arcade/Helix SDK packages into a single flat folder so darc actually finds them. (Only these two are
# arcade-produced dependencies tracked in Version.Details.xml; other tracked deps come from other repos
# and aren't in these artifacts.)
$flatFolder = Join-Path ([System.IO.Path]::GetTempPath()) "darc-packages-$([guid]::NewGuid())"
New-Item -ItemType Directory -Force -Path $flatFolder | Out-Null
$sdkPkgs = @(Get-ChildItem -Path $PackagesSource -Recurse -Include 'Microsoft.DotNet.Arcade.Sdk.*.nupkg', 'Microsoft.DotNet.Helix.Sdk.*.nupkg' -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notlike '*.symbols.nupkg' })
if ($sdkPkgs.Count -eq 0) {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "No Arcade/Helix SDK packages found under '$PackagesSource'."
  ExitWithExitCode 1
}
foreach ($p in $sdkPkgs) {
  $dest = Join-Path $flatFolder $p.Name
  if (-not (Test-Path $dest)) { Copy-Item -Path $p.FullName -Destination $dest }
}
Write-Host "Gathered $($sdkPkgs.Count) SDK package(s) into '$flatFolder' for darc:"
Get-ChildItem $flatFolder | ForEach-Object { Write-Host "  $($_.Name)" }

Write-Host "Updating dependencies from packages folder '$flatFolder' using darc..."
& $darc update-dependencies --packages-folder $flatFolder --ci
$darcExit = $LASTEXITCODE
Remove-Item -Recurse -Force $flatFolder -ErrorAction SilentlyContinue
if ($darcExit -ne 0) {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "darc update-dependencies --packages-folder failed."
  ExitWithExitCode 1
}

# Verify global.json now references the produced Arcade SDK version, so we fail loudly rather than
# silently building with the bootstrap SDK if darc updated nothing (e.g. a version match or a change
# in how arcade tracks its SDK).
$arcadePkg = Get-ChildItem -Path $PackagesSource -Recurse -Filter 'Microsoft.DotNet.Arcade.Sdk.*.nupkg' -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notlike '*.symbols.nupkg' } | Select-Object -First 1
if (-not $arcadePkg -or $arcadePkg.Name -notmatch '^Microsoft\.DotNet\.Arcade\.Sdk\.(.+)\.nupkg$') {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "Could not determine the produced Arcade SDK version from '$PackagesSource'."
  ExitWithExitCode 1
}
$version = $Matches[1]

$globalJsonPath = Join-Path $RepoRoot 'global.json'
$globalJson = Get-Content -Path $globalJsonPath -Raw
if ($globalJson -notmatch ([regex]::Escape('"Microsoft.DotNet.Arcade.Sdk"') + '\s*:\s*"' + [regex]::Escape($version) + '"')) {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "darc did not update global.json to the produced Arcade SDK version '$version'."
  ExitWithExitCode 1
}
Write-Host "Verified global.json now references the newly built Arcade SDK '$version'."

ExitWithExitCode 0
