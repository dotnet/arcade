# Points the repo at a locally built Arcade/Helix SDK so a subsequent `build -sign` uses the newly
# built SignTool. Cross-platform and dependency-free (pure file edits): it does NOT run dotnet or
# darc, so it works on the Windows/Linux/macOS signing agents (unlike eng/update-packagesource.ps1,
# which relies on Windows-oriented tools.ps1 helpers).
#
# Unlike the self-build's full `darc update-dependencies`, the signing validation only needs the
# Arcade/Helix SDK versions bumped and the local feed added; it does not need a coherent update of
# the rest of Version.Details.

Param(
  [Parameter(Mandatory=$true)][string] $PackagesSource  # Folder containing the freshly built *.nupkg (e.g. the downloaded build artifacts).
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path

# Discover the produced Arcade SDK version from the built package.
$arcadePkg = Get-ChildItem -Path $PackagesSource -Recurse -Filter 'Microsoft.DotNet.Arcade.Sdk.*.nupkg' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $arcadePkg) {
  throw "Could not find Microsoft.DotNet.Arcade.Sdk.*.nupkg under '$PackagesSource'."
}
if ($arcadePkg.Name -notmatch '^Microsoft\.DotNet\.Arcade\.Sdk\.(.+)\.nupkg$') {
  throw "Unexpected Arcade SDK package name '$($arcadePkg.Name)'."
}
$version = $Matches[1]
Write-Host "Using locally built Arcade/Helix SDK version '$version'."

# Bump the msbuild-sdks versions in global.json (targeted replace to preserve formatting).
$globalJsonPath = Join-Path $repoRoot 'global.json'
$globalJson = Get-Content -Path $globalJsonPath -Raw
$globalJson = $globalJson -replace '("Microsoft\.DotNet\.Arcade\.Sdk"\s*:\s*")[^"]*(")', "`${1}$version`${2}"
$globalJson = $globalJson -replace '("Microsoft\.DotNet\.Helix\.Sdk"\s*:\s*")[^"]*(")', "`${1}$version`${2}"
Set-Content -Path $globalJsonPath -Value $globalJson -NoNewline
Write-Host "Updated Arcade/Helix SDK versions in '$globalJsonPath'."

# Add the local package feed to NuGet.config so the new SDK resolves.
$nugetConfigPath = Join-Path $repoRoot 'NuGet.config'
Write-Host "Adding '$PackagesSource' to '$nugetConfigPath'..."
$nugetConfig = New-Object System.Xml.XmlDocument
$nugetConfig.PreserveWhitespace = $true
$nugetConfig.Load($nugetConfigPath)
$packageSources = $nugetConfig.SelectSingleNode("//packageSources")
$newSource = $nugetConfig.CreateElement("add")
$keyAttribute = $nugetConfig.CreateAttribute("key")
$keyAttribute.Value = "arcade-local"
$valueAttribute = $nugetConfig.CreateAttribute("value")
$valueAttribute.Value = $PackagesSource
$newSource.Attributes.Append($keyAttribute) | Out-Null
$newSource.Attributes.Append($valueAttribute) | Out-Null
$packageSources.AppendChild($newSource) | Out-Null
$nugetConfig.Save($nugetConfigPath)

Write-Host "done."
