param(
  [Parameter(Mandatory=$true)][string] $PromoteToChannels            # List of channels that the build should be promoted to
)

try {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 2.0

  # `tools.ps1` checks $ci to perform some actions. Since the post-build
  # scripts don't necessarily execute in the same agent that run the
  # build.ps1/sh script this variable isn't automatically set.
  $ci = $true
  $disableConfigureToolsetImport = $true
  . $PSScriptRoot\..\tools.ps1

  if ($PromoteToChannels -eq "") {
    Write-PipelineTaskError -Type 'warning' -Message "This build won't publish assets as it's not configured to any Maestro channel. If that wasn't intended use Darc to configure a default channel using add-default-channel for this branch or to promote it to a channel using add-build-to-channel. See https://github.com/dotnet/arcade/blob/master/Documentation/Darc.md#assigning-an-individual-build-to-a-channel for more info."
    ExitWithExitCode 0
  }

  Write-Host 'done.'
} 
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Category 'CheckChannelConsistency' -Message "There was an error while trying to check consistency of Maestro default channels for the build and post-build YAML configuration."
  ExitWithExitCode 1
}
