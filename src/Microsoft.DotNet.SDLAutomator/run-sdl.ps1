Param(
  [string] $GuardianCliLocation,
  [string] $Repository,
  [string] $WorkingDirectory,
  [string] $GdnFolder,
  [string] $ToolList,
  [string] $DncEngAccessToken,
  [string] $UpdateBaseline,
  [string] $GdnLoggerLevel="Standard"
)

foreach ($tool in $ToolList) {
  Write-Host "$GuardianCliLocation run --working-directory $WorkingDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel"
  Invoke-Expression "$GuardianCliLocation run --working-directory $WorkingDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian run $tool failed with exit code $LASTEXITCODE."
  }
}

if ($UpdateBaseline) {
  Invoke-Expression "$(Join-Path $PSScriptRoot "push-gdn.ps1") -Repository $Repository -WorkingDirectory $WorkingDirectory -GdnFolder $GdnFolder -DncEngAccessToken $DncEngAccessToken -PushReason `"Update baseline`""
}