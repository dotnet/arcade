Param(
  [string] $GuardianCliLocation,            # Required: the path to the guardian CLI executable
  [string] $Repository,                     # Required: the name of the repository (e.g. dotnet/arcade)
  [string] $SourceDirectory,                # Required: the directory where source files are located
  [string] $ArtifactsDirectory,             # Required: the directory where build artifacts are located
  [string] $DncEngAccessToken,              # Required: access token for dnceng; should be provided via KeyVault
  [string[]] $SourceToolsList,              # Required: list of SDL tools to run on source code
  [string[]] $ArtifactToolsList,            # Required: list of SDL tools to run on built artifacts
  [bool] $UpdateBaseline=$False,            # Optional: if true, will update the baseline in the repository; should only be run after fixing any issues which need to be fixed
  [bool] $TsaOnboard=$False,                # Optional: if true, will onboard the repository to TSA; should only be run once
  [string] $TsaCodebaseName,                # Optional: only needed if TsaOnboard is true; the name of the codebase registered with TSA
  [string] $TsaNotificationEmail,           # Optional: only needed if TsaOnboard is true; the email(s) which will receive notifications of TSA bug filings (e.g. alias@microsoft.com)
  [string] $TsaCodebaseAdmin,               # Optional: only needed if TsaOnboard is true; the aliases which are admins of the TSA codebase (e.g. DOMAIN\alias)
  [string] $TsaBugAreaPath,                 # Optional: only needed if TsaOnboard is true; the area path where TSA will file bugs in AzDO
  [string] $GuardianLoggerLevel="Standard"  # Optional: the logger level for the Guardian CLI; options are Trace, Verbose, Standard, Warning, and Error
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
