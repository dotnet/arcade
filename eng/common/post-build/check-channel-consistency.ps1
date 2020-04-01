param(
  [Parameter(Mandatory=$true)][string] $PromoteToChannels,            # List of channels that the build should be promoted to
  [Parameter(Mandatory=$true)][array] $AvailableChannelIds            # List of channel IDs available in the YAML implementation
)

try {
  . $PSScriptRoot\post-build-utils.ps1

  if ($PromoteToChannels -eq "") {
    Write-PipelineTaskError -Type 'warning' -Message "This build won't publish asset as it's not configured to any Maestro channel. If that wasn't intended use Darc to configure a default channel for the build or to promote it to a channel."
    ExitWithExitCode 0
  }

  # Check that every channel that Maestro told to promote the build to 
  # is available in YAML
  $PromoteToChannelsIds = $PromoteToChannels -split "\D" | Where-Object { $_ }

  foreach ($id in $PromoteToChannelsIds) {
    if (($id -ne 0) -and ($id -notin $AvailableChannelIds)) {
      Write-PipelineTaskError -Type 'warning' -Message "Channel $id is not present in the post-build YAML configuration!"
    }
  }

  Write-Host 'done.'
} 
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "There was an error while trying to check consistency of Maestro default channels for the build and post-build YAML configuration."
  ExitWithExitCode 1
}
