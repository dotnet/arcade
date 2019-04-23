Param(
  [string] $GuardianCliLocation,
  [string] $Repository,
  [string] $SourceDirectory,
  [string] $ArtifactsDirectory,
  [string] $DncEngAccessToken,
  [string[]] $SourceToolsList, # tools which run on source code
  [string[]] $ArtifactToolsList, # tools which run on built artifacts
  [bool] $UpdateBaseline=$False,
  [string] $GdnLoggerLevel="Standard"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$gdnFolder = Invoke-Expression "$(Join-Path $PSScriptRoot "init-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -DncEngAccessToken $DncEngAccessToken -GdnLoggerLevel $GdnLoggerLevel"
Invoke-Expression "$(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -GdnFolder $gdnFolder -ToolList $ArtifactsToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GdnLoggerLevel $GdnLoggerLevel"
Copy-Item -Recurse -Force $gdnFolder $SourceDirectory
$gdnFolder = Join-Path $SourceDirectory ".gdn"
Invoke-Expression "$(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $SourceDirectory -GdnFolder $gdnFolder -ToolList $SourceToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GdnLoggerLevel $GdnLoggerLevel"
