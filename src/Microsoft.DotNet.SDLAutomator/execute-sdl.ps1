Param(
  [string] $GuardianCliLocation,
  [string] $Repository,
  [string] $SourceDirectory,
  [string] $ArtifactsDirectory,
  [string] $DncEngAccessToken,
  [string[]] $SourceToolsList, # tools which run on source code
  [string[]] $ArtifactToolsList, # tools which run on built artifacts
  [bool] $UpdateBaseline=$False,
  [bool] $TsaOnboard=$False,
  [string] $TsaCodebaseName,
  [string] $TsaNotificationEmail,
  [string] $TsaCodebaseAdmin,
  [string] $TsaBugAreaPath,
  [string] $GuardianLoggerLevel="Standard"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$gdnFolder = Invoke-Expression "$(Join-Path $PSScriptRoot "init-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -DncEngAccessToken $DncEngAccessToken -GuardianLoggerLevel $GuardianLoggerLevel"

if ($TsaOnboard) {
  if ($TsaCodebaseName -and $TsaNotificationEmail -and $TsaCodebaseAdmin -and $TsaBugAreaPath) {
    Write-Host "$GuardianCliLocation tsa-onboard --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"https://dev.azure.com/dnceng/`" --project-name `"internal`" --area-path `"$TsaBugAreaPath`" --working-directory $WorkingDirectory -logger-level $GuardianLoggerLevel"
    Invoke-Expression "$GuardianCliLocation tsa-onboard --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"https://dev.azure.com/dnceng/`" --project-name `"internal`" --area-path `"$TsaBugAreaPath`" --working-directory $WorkingDirectory -logger-level $GuardianLoggerLevel"
    if ($LASTEXITCODE -ne 0) {
      Write-Error "Guardian tsa-onboard failed with exit code $LASTEXITCODE."
    }
  } else {
    Write-Error "Could not onboard to TSA -- not all required values ($$TsaCodebaseName, $$TsaNotificationEmail, $$TsaCodebaseAdmin, $$TsaBugAreaPath) were specified."
  }
}

Invoke-Expression "$(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -GdnFolder $gdnFolder -ToolList $ArtifactsToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel"
Copy-Item -Recurse -Force $gdnFolder $SourceDirectory
$gdnFolder = Join-Path $SourceDirectory ".gdn"
Invoke-Expression "$(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $SourceDirectory -GdnFolder $gdnFolder -ToolList $SourceToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel"
