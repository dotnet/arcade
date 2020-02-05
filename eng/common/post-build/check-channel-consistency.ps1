param(
  [Parameter(Mandatory=$true)][string] $InitialChannels,            # List of channels that the build should be promoted to
  [Parameter(Mandatory=$true)][int] $PromoteToMaestroChannelId      # If the build should be promoted to a channel this will have the Channel ID
)

try {
  . $PSScriptRoot\post-build-utils.ps1

  $AvailableChannelIds = 2, 9, 131, 529, 548, 549, 551, 562, 678, 679

  if ($InitialChannels -eq "[] ,") {
    Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "This build won't publish to any channel. Maestro didn't return any default channel."
  }

  # Check that every channel that Maestro told to promote the build to 
  # is available in YAML
  $InitialChannelsIds = $InitialChannels -split "\D" | Where-Object { $_ }

  foreach ($id in $InitialChannelsIds) {
    if ($id -notin $AvailableChannelIds) {
      Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "Channel $id is not present in the post-build YAML configuration!"
      ExitWithExitCode 1
    }
  }

  if ($PromoteToMaestroChannelId -ne 0) {
    if ($PromoteToMaestroChannelId -notin $AvailableChannelIds) {
      Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "Channel $PromoteToMaestroChannelId is not present in the post-build YAML configuration!"
      ExitWithExitCode 1
    }
  }

  Write-Host 'done.'
} 
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "There was an error while trying to check consistency of Maestro default channels for the build and post-build YAML configuration."
  ExitWithExitCode 1
}
