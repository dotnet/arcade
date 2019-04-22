Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $DncengPat,
  [string[]] $ToolsList,
  [bool] $UpdateBaseline=$False,
  [string] $GdnLoggerLevel="Standard"
)

foreach ($tool in $ToolsList) {
  Write-Host "guardian run --working-directory $SourcesDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel"
  guardian run --working-directory $SourcesDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GdnLoggerLevel
}

if ($UpdateBaseline) {
  Invoke-Expression "push-gdn.ps1 -Repository $Repository -SourcesDirectory $SourcesDirectory -GdnFolder $SourcesDirectory/.gdn -DncengPat $DncengPat -PushReason `"Update baseline`""
}