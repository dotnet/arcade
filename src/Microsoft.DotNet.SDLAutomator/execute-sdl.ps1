Param(
  [string] $GuardianCliLocation,                    # Required: the path to the guardian CLI executable
  [string] $Repository,                             # Required: the name of the repository (e.g. dotnet/arcade)
  [string] $SourceDirectory,                        # Required: the directory where source files are located
  [string] $ArtifactsDirectory,                     # Required: the directory where build artifacts are located
  [string] $DncEngAccessToken,                      # Required: access token for dnceng; should be provided via KeyVault
  [string[]] $SourceToolsList,                      # Required: list of SDL tools to run on source code
  [string[]] $ArtifactToolsList,                    # Required: list of SDL tools to run on built artifacts
  [bool] $TsaPublish=$False,                        # Required: true will publish results to TSA; only set to true after onboarding to TSA
  [string] $BranchName=$env:BUILD_SOURCEBRANCHNAME, # Optional: required for TSA publish; defaults to $(Build.SourceBranchName)
  [string] $BuildNumber=$env:BUILD_BUILDNUMBER,     # Optional: required for TSA publish; defaults to $(Build.BuildNumber)
  [bool] $UpdateBaseline=$False,                    # Optional: if true, will update the baseline in the repository; should only be run after fixing any issues which need to be fixed
  [bool] $TsaOnboard=$False,                        # Optional: if true, will onboard the repository to TSA; should only be run once
  [string] $TsaCodebaseName,                        # Optional: only needed if TsaOnboard is true; the name of the codebase registered with TSA
  [string] $TsaNotificationEmail,                   # Optional: only needed if TsaOnboard is true; the email(s) which will receive notifications of TSA bug filings (e.g. alias@microsoft.com)
  [string] $TsaCodebaseAdmin,                       # Optional: only needed if TsaOnboard is true; the aliases which are admins of the TSA codebase (e.g. DOMAIN\alias)
  [string] $TsaBugAreaPath,                         # Optional: only needed if TsaOnboard is true; the area path where TSA will file bugs in AzDO
  [string] $GuardianLoggerLevel="Standard"          # Optional: the logger level for the Guardian CLI; options are Trace, Verbose, Standard, Warning, and Error
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

& $(Join-Path $PSScriptRoot "init-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -DncEngAccessToken $DncEngAccessToken -GuardianLoggerLevel $GuardianLoggerLevel
$gdnFolder = Join-Path $ArtifactsDirectory ".gdn"

if ($TsaOnboard) {
  if ($TsaCodebaseName -and $TsaNotificationEmail -and $TsaCodebaseAdmin -and $TsaBugAreaPath) {
    Write-Host "$GuardianCliLocation tsa-onboard --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"https://dev.azure.com/dnceng/`" --project-name `"internal`" --area-path `"$TsaBugAreaPath`" --working-directory $ArtifactsDirectory --logger-level $GuardianLoggerLevel"
    Invoke-Expression "$GuardianCliLocation tsa-onboard --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"https://dev.azure.com/dnceng/`" --project-name `"internal`" --area-path `"$TsaBugAreaPath`" --working-directory $ArtifactsDirectory --logger-level $GuardianLoggerLevel"
    if ($LASTEXITCODE -ne 0) {
      Write-Error "Guardian tsa-onboard failed with exit code $LASTEXITCODE."
    }
  } else {
    Write-Error "Could not onboard to TSA -- not all required values ($$TsaCodebaseName, $$TsaNotificationEmail, $$TsaCodebaseAdmin, $$TsaBugAreaPath) were specified."
  }
}

if ($ArtifactToolsList -and $ArtifactToolsList.Count -gt 0) {
  & $(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -TargetDirectory $ArtifactsDirectory -GdnFolder $gdnFolder -ToolsList $ArtifactToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel
}
if ($SourceToolsList -and $SourceToolsList.Count -gt 0) {
  & $(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $GuardianCliLocation -Repository $Repository -WorkingDirectory $ArtifactsDirectory -TargetDirectory $SourceDirectory -GdnFolder $gdnFolder -ToolsList $SourceToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel
}

if ($UpdateBaseline) {
  & (Join-Path $PSScriptRoot "push-gdn.ps1") -Repository $Repository -GdnFolder $GdnFolder -DncEngAccessToken $DncEngAccessToken -PushReason "Update baseline"
}

if ($TsaPublish) {
  if ($BranchName -and $BuildNumber) {
    $TsaRepositoryName = $Repository.Replace("/", "-")
    Write-Host "$GuardianCliLocation tsa-publish --all-tools --repository-name $TsaRepositoryName --branch-name $BranchName --build-number $BuildNumber --working-directory $SourceDirectory --logger-level $GuardianLoggerLevel"
    Invoke-Expression "$GuardianCliLocation tsa-publish --all-tools --repository-name $TsaRepositoryName --branch-name $BranchName --build-number $BuildNumber --working-directory $SourceDirectory --logger-level $GuardianLoggerLevel"
    if ($LASTEXITCODE -ne 0) {
      Write-Error "Guardian tsa-publish failed with exit code $LASTEXITCODE."
    }
  } else {
    Write-Error "Could not publish to TSA -- not all required values ($$BranchName, $$BuildNumber) were specified."
  }
}