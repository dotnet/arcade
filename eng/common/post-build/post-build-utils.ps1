$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$MaestroEndpoint = "https://maestro-int.westus2.cloudapp.azure.com"
$MaestroApiVersion = "2019-01-16"

# `tools.ps1` checks $ci to perform some actions. Since the post-build
# scripts don't necessarily execute in the same agent that run the
# build.ps1/sh script this variable isn't automatically set.
$ci = $true
. $PSScriptRoot\..\tools.ps1

function CheckExitCode([string]$Stage) {
  $exitCode = $LASTEXITCODE

  if ($exitCode -ne 0) {
    Write-PipelineTaskError "Something failed while '$Stage'. Check for errors above. Exiting now..."
    ExitWithExitCode $exitCode
  }
}

function Create-MaestroApiRequestHeaders([string]$ContentType = "application/json", [string]$AuthToken) {
  $headers = New-Object 'System.Collections.Generic.Dictionary[[String],[String]]'
  $headers.Add('Accept', $ContentType)
  $headers.Add('Authorization',"Bearer $AuthToken")
  return $headers
}

function Get-MaestroChannel([int]$ChannelId, [string]$MaestroAccessToken) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroEndpoint/api/channels/${ChannelId}?api-version=$MaestroApiVersion"
  
  $result = try { Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json } catch { Write-Host "Error: $_" }
  return $result
}

function Get-MaestroBuild([int]$BuildId, [string]$MaestroAccessToken) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroEndpoint/api/builds/${BuildId}?api-version=$MaestroApiVersion"
  $result = try { return Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json } catch { Write-Host "Error: $_" }
  return $result
}

function Get-MaestroSubscriptions([string]$SourceRepository, [int]$ChannelId, [string]$MaestroAccessToken) {
  $SourceRepository = [System.Web.HttpUtility]::UrlEncode($SourceRepository) 
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroEndpoint/api/subscriptions?sourceRepository=$SourceRepository&channelId=$ChannelId&api-version=$MaestroApiVersion"
  
  $result = Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json
  return $result
}

function Trigger-Subscription([string]$SubscriptionId, [string]$MaestroAccessToken) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroEndpoint/api/subscriptions/$SubscriptionId/trigger?api-version=$MaestroApiVersion"
  Invoke-WebRequest -Uri $apiEndpoint -Headers $apiHeaders -Method Post | Out-Null
}

function Assign-BuildToChannel([int]$BuildId, [int]$ChannelId, [string]$MaestroAccessToken) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$maestroEndpoint/api/channels/${ChannelId}/builds/${BuildId}?api-version=$MaestroApiVersion"
  Invoke-WebRequest -Method Post -Uri $apiEndpoint -Headers $apiHeaders | Out-Null
}
