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
# pure-file-edit path in use-built-sdk.ps1 instead. Must run with Maestro/BAR authentication available
# (darc update-dependencies resolves asset locations from BAR) - the pipeline runs it under AzureCLI
# with the Maestro service connection so darc's AzureCliCredential can get a token.

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

# arcade produces ONLY nonshipping packages, so Microsoft.DotNet.Arcade.Sdk and Microsoft.DotNet.Helix.Sdk
# both live in the same packages/<config>/NonShipping folder. darc's --packages-folder scan is
# NON-recursive (Directory.GetFiles(path, "*.nupkg")), so point it at that folder - i.e. the directory
# containing the produced Arcade SDK package - rather than at the artifacts root (which nests the
# packages and would make darc report "Found no dependencies to update").
$arcadePkg = Get-ChildItem -Path $PackagesSource -Recurse -Filter 'Microsoft.DotNet.Arcade.Sdk.*.nupkg' -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -notlike '*.symbols.nupkg' } | Select-Object -First 1
if (-not $arcadePkg -or $arcadePkg.Name -notmatch '^Microsoft\.DotNet\.Arcade\.Sdk\.(.+)\.nupkg$') {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "Could not find Microsoft.DotNet.Arcade.Sdk.*.nupkg under '$PackagesSource'."
  ExitWithExitCode 1
}
$version = $Matches[1]
$packagesFolder = $arcadePkg.DirectoryName

Write-Host "Updating dependencies from packages folder '$packagesFolder' using darc..."
& $darc update-dependencies --packages-folder $packagesFolder --ci
if ($LASTEXITCODE -ne 0) {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "darc update-dependencies --packages-folder failed."
  ExitWithExitCode 1
}

# Verify global.json now references the produced Arcade SDK version, so we fail loudly rather than
# silently building with the bootstrap SDK if darc updated nothing.
$globalJsonPath = Join-Path $RepoRoot 'global.json'
$globalJson = Get-Content -Path $globalJsonPath -Raw
if ($globalJson -notmatch ([regex]::Escape('"Microsoft.DotNet.Arcade.Sdk"') + '\s*:\s*"' + [regex]::Escape($version) + '"')) {
  Write-PipelineTelemetryError -Category 'SelfBuild' -Message "darc did not update global.json to the produced Arcade SDK version '$version'."
  ExitWithExitCode 1
}
Write-Host "Verified global.json now references the newly built Arcade SDK '$version'."

ExitWithExitCode 0
