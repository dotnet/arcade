Param(
  [string] $GuardianCliLocation,
  [string] $Repository,
  [string] $WorkingDirectory,
  [string] $TargetDirectory,
  [string] $GdnFolder,
  [string[]] $ToolsList,
  [string] $UpdateBaseline,
  [string] $GuardianLoggerLevel="Standard"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

# We store config files in the r directory of .gdn
Write-Host $ToolsList
$gdnConfigPath = Join-Path $GdnFolder "r"
$gdnConfig = ""

foreach ($tool in $ToolsList) {
  $gdnConfigFile = Join-Path $gdnConfigPath "$tool-configure.gdnconfig"
  Write-Host $tool
  # We have to manually configure tools that run on source to look at the source directory only
  if ($tool -eq "credscan") {
    Write-Host "$GuardianCliLocation configure --working-directory $WorkingDirectory --tool $tool --args `"TargetDirectory : $TargetDirectory`" --output-path $gdnConfigFile --logger-level $GuardianLoggerLevel --noninteractive"
    &$GuardianCliLocation configure --working-directory $WorkingDirectory --tool $tool --args `"TargetDirectory : $TargetDirectory`" --output-path $gdnConfigFile --logger-level $GuardianLoggerLevel --noninteractive
    $gdnConfig = "--config $gdnConfigFile"
  }
  if ($tool -eq "policheck") {
    Write-Host "$GuardianCliLocation configure --working-directory $WorkingDirectory --tool $tool --args `"Target : $TargetDirectory`" --output-path $gdnConfigFile --logger-level $GuardianLoggerLevel --noninteractive"
    &$GuardianCliLocation configure --working-directory $WorkingDirectory --tool $tool --args `"Target : $TargetDirectory`" --output-path $gdnConfigFile --logger-level $GuardianLoggerLevel --noninteractive
    $gdnConfig = "--config $gdnConfigFile"
  }

  Write-Host "$GuardianCliLocation run --working-directory $WorkingDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GuardianLoggerLevel $gdnConfig"
  &$GuardianCliLocation run --working-directory $WorkingDirectory --tool $tool --baseline mainbaseline --update-baseline $UpdateBaseline --logger-level $GuardianLoggerLevel $gdnConfig
}