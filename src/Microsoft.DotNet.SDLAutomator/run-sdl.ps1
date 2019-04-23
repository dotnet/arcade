Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $ArtifactsDirectory,
  [string] $DncEngAccessToken,
  [string[]] $SourceToolsList, # tools which run on source code
  [string[]] $ArtifactToolsList, # tools which run on built artifacts
  [bool] $UpdateBaseline=$False,
  [string] $GdnLoggerLevel="Standard"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

foreach ($tool in $ToolsList) {
  Write-Host "guardian run --working-directory $SourcesDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel"
  guardian run --working-directory $SourcesDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian run $tool failed with exit code $LASTEXITCODE."
  }
}

if ($UpdateBaseline) {
  Invoke-Expression "$PSScriptRoot\push-gdn.ps1 -Repository $Repository -SourcesDirectory $SourcesDirectory -GdnFolder $SourcesDirectory/.gdn -DncEngAccessToken $DncEngAccessToken -PushReason `"Update baseline`""
}