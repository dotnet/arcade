param(
  [Parameter(Mandatory=$true)][int] $BuildId,
  [Parameter(Mandatory=$true)][int] $ChannelId,
  [Parameter(Mandatory=$true)][string] $MaestroApiAccessToken,
  [Parameter(Mandatory=$false)][string] $MaestroApiEndPoint = 'https://maestro.dot.net',
  [Parameter(Mandatory=$false)][string] $MaestroApiVersion = '2019-01-16'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

try {
  # `tools.ps1` checks $ci to perform some actions. Since the post-build
  # scripts don't necessarily execute in the same agent that run the
  # build.ps1/sh script this variable isn't automatically set.
  $ci = $true
  $disableConfigureToolsetImport = $true
  . $PSScriptRoot\..\tools.ps1

  $darc = Get-Darc

  # Check that the channel we are going to promote the build to exist
  & $darc get-channel `
    --id $ChannelId `
    --azdev-pat $AzdoToken `
    --bar-uri $MaestroApiEndPoint `
    --password $MaestroApiAccessToken

  if(-not $?) {
    Write-PipelineTelemetryCategory -Category 'PromoteBuild' -Message "Channel with BAR ID $ChannelId was not found in BAR!"
    ExitWithExitCode 1
  }

  # Get info about which channel(s) the build has already been promoted to
  $buildInfo = & $darc get-build `
    --id $BuildId `
    --output-format json `
    --azdev-pat $AzdoToken `
    --bar-uri $MaestroApiEndPoint `
    --password $MaestroApiAccessToken `
    | ConvertFrom-Json

  if (-not $?) {
    Write-PipelineTelemetryError -Category 'PromoteBuild' -Message "Build with BAR ID $BuildId was not found in BAR!"
    ExitWithExitCode 1
  }

  # Find whether the build is already assigned to the channel or not
  if ($buildInfo.channels) {
    foreach ($channel in $buildInfo.channels) {
      if ($channel.Id -eq $ChannelId) {
        Write-Host "The build with BAR ID $BuildId is already on channel $ChannelId!"
        ExitWithExitCode 0
      }
    }
  }

  Write-Host "Promoting build '$BuildId' to channel '$ChannelId'."

  & $darc add-build-to-channel `
    --id $BuildId `
    --channel $ChannelId `
    --azdev-pat $AzdoToken `
    --bar-uri $MaestroApiEndPoint `
    --password $MaestroApiAccessToken `

  Write-Host 'done.'
} 
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Category 'PromoteBuild' -Message "There was an error while trying to promote build '$BuildId' to channel '$ChannelId'"
  ExitWithExitCode 1
}
