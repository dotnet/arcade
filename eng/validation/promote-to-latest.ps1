# Promotes an Arcade build to the release channel ('.NET Eng - Latest') using the current/default
# Arcade. This is the actual promotion gate; it runs after a successful build, and (when validation
# is enabled) after the ValidateSdk stage passes. Assets were already published to the source
# channel during the build's post-build publishing, so this uses --skip-assets-publishing.
#
# Mirrors dotnet/arcade-validation's eng/validation/update-channel.ps1.

Param(
  [Parameter(Mandatory=$true)][int]    $BuildId,     # BAR build id of the build to promote.
  [Parameter(Mandatory=$true)][string] $AzdoToken,
  [string] $FinalChannel = '.NET Eng - Latest'
)

set-strictmode -version 2.0
$ErrorActionPreference = 'Stop'

. $PSScriptRoot\..\common\tools.ps1

$darcPath = Join-Path $PSScriptRoot "darc\$(New-Guid)"
& $PSScriptRoot\..\common\darc-init.ps1 -toolpath $darcPath | Out-Host
$darc = Join-Path $darcPath 'darc.exe'

try {
  Write-Host "Promoting build $BuildId to '$FinalChannel'."
  & $darc add-build-to-channel `
      --id $BuildId `
      --channel "$FinalChannel" `
      --azdev-pat $AzdoToken `
      --ci `
      --skip-assets-publishing
  if ($LASTEXITCODE -ne 0) { throw "darc add-build-to-channel to '$FinalChannel' failed." }

  Write-Host 'done.'
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'PromoteToLatest' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
