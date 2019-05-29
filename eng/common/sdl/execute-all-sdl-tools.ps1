Param(
  [string] $GuardianPackageName,                        # Required: the name of guardian CLI pacakge (not needed if GuardianCliLocation is specified)
  [string] $NugetPackageDirectory,                      # Required: directory where NuGet packages are installed (not needed if GuardianCliLocation is specified)
  [string] $GuardianCliLocation,                        # Optional: Direct location of Guardian CLI executable if GuardianPackageName & NugetPackageDirectory are not specified
  [string] $Repository,                                 # Required: the name of the repository (e.g. dotnet/arcade)
  [string] $BranchName="master",                        # Optional: name of branch or version of gdn settings; defaults to master
  [string] $SourceDirectory,                            # Required: the directory where source files are located
  [string] $ArtifactsDirectory,                         # Required: the directory where build artifacts are located
  [string] $DncEngAccessToken,                          # Required: access token for dnceng; should be provided via KeyVault
  [string[]] $SourceToolsList,                          # Optional: list of SDL tools to run on source code
  [string[]] $ArtifactToolsList,                        # Optional: list of SDL tools to run on built artifacts
  [bool] $TsaPublish=$False,                            # Optional: true will publish results to TSA; only set to true after onboarding to TSA
  [string] $TsaBranchName=$env:BUILD_SOURCEBRANCHNAME,  # Optional: required for TSA publish; defaults to $(Build.SourceBranchName)
  [string] $TsaRepositoryName,                          # Optional: TSA repository name; will be generated automatically if not submitted
  [string] $BuildNumber=$env:BUILD_BUILDNUMBER,         # Optional: required for TSA publish; defaults to $(Build.BuildNumber)
  [bool] $UpdateBaseline=$False,                        # Optional: if true, will update the baseline in the repository; should only be run after fixing any issues which need to be fixed
  [bool] $TsaOnboard=$False,                            # Optional: if true, will onboard the repository to TSA; should only be run once
  [string] $TsaInstanceUrl,                             # Optional: only needed if TsaOnboard or TsaPublish is true; the instance-url registered with TSA
  [string] $TsaCodebaseName,                            # Optional: only needed if TsaOnboard or TsaPublish is true; the name of the codebase registered with TSA
  [string] $TsaProjectName,                             # Optional: only needed if TsaOnboard or TsaPublish is true; the name of the project registered with TSA
  [string] $TsaNotificationEmail,                       # Optional: only needed if TsaOnboard is true; the email(s) which will receive notifications of TSA bug filings (e.g. alias@microsoft.com)
  [string] $TsaCodebaseAdmin,                           # Optional: only needed if TsaOnboard is true; the aliases which are admins of the TSA codebase (e.g. DOMAIN\alias)
  [string] $TsaBugAreaPath,                             # Optional: only needed if TsaOnboard is true; the area path where TSA will file bugs in AzDO
  [string] $TsaIterationPath,                           # Optional: only needed if TsaOnboard is true; the iteration path where TSA will file bugs in AzDO
  [string] $GuardianLoggerLevel="Standard"              # Optional: the logger level for the Guardian CLI; options are Trace, Verbose, Standard, Warning, and Error
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0
$LASTEXITCODE = 0

$RepoName = $Repository -replace '(.*?)-(.*)', '$1/$2';

if ($GuardianPackageName) {
  $guardianCliLocation = Join-Path $NugetPackageDirectory (Join-Path $GuardianPackageName (Join-Path "tools" "guardian.cmd"))
} else {
  $guardianCliLocation = $GuardianCliLocation
}

& $(Join-Path $PSScriptRoot "init-sdl.ps1") -GuardianCliLocation $guardianCliLocation -Repository $RepoName -BranchName $BranchName -WorkingDirectory $ArtifactsDirectory -DncEngAccessToken $DncEngAccessToken -GuardianLoggerLevel $GuardianLoggerLevel
$gdnFolder = Join-Path $ArtifactsDirectory ".gdn"

if ($TsaOnboard) {
  if ($TsaCodebaseName -and $TsaNotificationEmail -and $TsaCodebaseAdmin -and $TsaBugAreaPath) {
    Write-Host "$guardianCliLocation tsa-onboard --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"$TsaInstanceUrl`" --project-name `"$TsaProjectName`" --area-path `"$TsaBugAreaPath`" --iteration-path `"$TsaIterationPath`" --working-directory $ArtifactsDirectory --logger-level $GuardianLoggerLevel"
    & $guardianCliLocation tsa-onboard --codebase-name "$TsaCodebaseName" --notification-alias "$TsaNotificationEmail" --codebase-admin "$TsaCodebaseAdmin" --instance-url "$TsaInstanceUrl" --project-name "$TsaProjectName" --area-path "$TsaBugAreaPath" --iteration-path "$TsaIterationPath" --working-directory $ArtifactsDirectory --logger-level $GuardianLoggerLevel
    if ($LASTEXITCODE -ne 0) {
      Write-Error "Guardian tsa-onboard failed with exit code $LASTEXITCODE."
    }
  } else {
    Write-Error "Could not onboard to TSA -- not all required values ($$TsaCodebaseName, $$TsaNotificationEmail, $$TsaCodebaseAdmin, $$TsaBugAreaPath) were specified."
  }
}

if ($ArtifactToolsList -and $ArtifactToolsList.Count -gt 0) {
  & $(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $guardianCliLocation -WorkingDirectory $ArtifactsDirectory -TargetDirectory $ArtifactsDirectory -GdnFolder $gdnFolder -ToolsList $ArtifactToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel
}
if ($SourceToolsList -and $SourceToolsList.Count -gt 0) {
  & $(Join-Path $PSScriptRoot "run-sdl.ps1") -GuardianCliLocation $guardianCliLocation -WorkingDirectory $ArtifactsDirectory -TargetDirectory $SourceDirectory -GdnFolder $gdnFolder -ToolsList $SourceToolsList -DncEngAccessToken $DncEngAccessToken -UpdateBaseline $UpdateBaseline -GuardianLoggerLevel $GuardianLoggerLevel
}

if ($UpdateBaseline) {
  & (Join-Path $PSScriptRoot "push-gdn.ps1") -Repository $RepoName -BranchName $BranchName -GdnFolder $GdnFolder -DncEngAccessToken $DncEngAccessToken -PushReason "Update baseline"
}

if ($TsaPublish) {
  if ($TsaBranchName -and $BuildNumber) {
    if (-not $TsaRepositoryName) {
      $TsaRepositoryName = "$($Repository)-$($BranchName)"
    }
    Write-Host "$guardianCliLocation tsa-publish --all-tools --repository-name `"$TsaRepositoryName`" --branch-name `"$TsaBranchName`" --build-number `"$BuildNumber`" --codebase-name `"$TsaCodebaseName`" --notification-alias `"$TsaNotificationEmail`" --codebase-admin `"$TsaCodebaseAdmin`" --instance-url `"$TsaInstanceUrl`" --project-name `"$TsaProjectName`" --area-path `"$TsaBugAreaPath`" --iteration-path `"$TsaIterationPath`" --working-directory $SourceDirectory --logger-level $GuardianLoggerLevel"
    & $guardianCliLocation tsa-publish --all-tools --repository-name "$TsaRepositoryName" --branch-name "$TsaBranchName" --build-number "$BuildNumber" --codebase-name "$TsaCodebaseName" --notification-alias "$TsaNotificationEmail" --codebase-admin "$TsaCodebaseAdmin" --instance-url "$TsaInstanceUrl" --project-name "$TsaProjectName" --area-path "$TsaBugAreaPath" --iteration-path "$TsaIterationPath" --working-directory $ArtifactsDirectory  --logger-level $GuardianLoggerLevel
    if ($LASTEXITCODE -ne 0) {
      Write-Error "Guardian tsa-publish failed with exit code $LASTEXITCODE."
    }
  } else {
    Write-Error "Could not publish to TSA -- not all required values ($$TsaBranchName, $$BuildNumber) were specified."
  }
}