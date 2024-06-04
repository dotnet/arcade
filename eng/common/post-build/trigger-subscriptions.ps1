param(
  [Parameter(Mandatory=$true)][string] $SourceRepo,
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

  $normalizedSourceRepo = $SourceRepo.Replace('dnceng@', '')

  # Get all the $SourceRepo subscriptions
  $subscriptions = & $darc get-subscriptions `
    --source-repo $normalizedSourceRepo `
    --channel $ChannelId `
    --output-format json `
    --azdev-pat $AzdoToken `
    --bar-uri $MaestroApiEndPoint `
    --password $MaestroApiAccessToken `
    | ConvertFrom-Json

  if (!$subscriptions) {
    Write-PipelineTelemetryError -Category 'TriggerSubscriptions' -Message "No subscriptions found for source repo '$normalizedSourceRepo' in channel '$ChannelId'"
    ExitWithExitCode 0
  }

  $subscriptionsToTrigger = New-Object System.Collections.Generic.List[string]
  $failedTriggeredSubscription = $false

  # Get all enabled subscriptions that need dependency flow on 'everyBuild'
  foreach ($subscription in $subscriptions) {
    if ($subscription.enabled -and $subscription.policy.updateFrequency -like 'everyBuild' -and $subscription.channel.id -eq $ChannelId) {
      Write-Host "Should trigger this subscription: ${$subscription.id}"
      [void]$subscriptionsToTrigger.Add($subscription.id)
    }
  }

  foreach ($subscriptionToTrigger in $subscriptionsToTrigger) {
    try {
      Write-Host "Triggering subscription '$subscriptionToTrigger'."

      & $darc trigger-subscriptions `
        --id $subscriptionToTrigger `
        --azdev-pat $AzdoToken `
        --bar-uri $MaestroApiEndPoint `
        --password $MaestroApiAccessToken

      Write-Host 'done.'
    } 
    catch
    {
      Write-Host "There was an error while triggering subscription '$subscriptionToTrigger'"
      Write-Host $_
      Write-Host $_.ScriptStackTrace
      $failedTriggeredSubscription = $true
    }
  }

  if ($subscriptionsToTrigger.Count -eq 0) {
    Write-Host "No subscription matched source repo '$normalizedSourceRepo' and channel ID '$ChannelId'."
  }
  elseif ($failedTriggeredSubscription) {
    Write-PipelineTelemetryError -Category 'TriggerSubscriptions' -Message 'At least one subscription failed to be triggered...'
    ExitWithExitCode 1
  }
  else {
    Write-Host 'All subscriptions were triggered successfully!'
  }
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'TriggerSubscriptions' -Message $_
  ExitWithExitCode 1
}
