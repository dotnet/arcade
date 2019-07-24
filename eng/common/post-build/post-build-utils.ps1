# Most of the functions in this file require the variables `MaestroApiEndPoint`, 
# `MaestroApiVersion` and `MaestroAccessToken` to be globally available.

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

# `tools.ps1` checks $ci to perform some actions. Since the post-build
# scripts don't necessarily execute in the same agent that run the
# build.ps1/sh script this variable isn't automatically set.
$ci = $true
. $PSScriptRoot\..\tools.ps1

function Create-MaestroApiRequestHeaders([string]$ContentType = "application/json") {
  $headers = New-Object 'System.Collections.Generic.Dictionary[[String],[String]]'
  $headers.Add('Accept', $ContentType)
  $headers.Add('Authorization',"Bearer $MaestroAccessToken")
  return $headers
}

function Get-MaestroChannel([int]$ChannelId) {
  $apiHeaders = Create-MaestroApiRequestHeaders
  $apiEndpoint = "$MaestroApiEndPoint/api/channels/${ChannelId}?api-version=$MaestroApiVersion"
  
  $result = try { Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json } catch { Write-Host "Error: $_" }
  return $result
}

function Get-MaestroBuild([int]$BuildId) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroApiEndPoint/api/builds/${BuildId}?api-version=$MaestroApiVersion"

  $result = try { return Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json } catch { Write-Host "Error: $_" }
  return $result
}

function Get-MaestroSubscriptions([string]$SourceRepository, [int]$ChannelId) {
  $SourceRepository = [System.Web.HttpUtility]::UrlEncode($SourceRepository) 
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroApiEndPoint/api/subscriptions?sourceRepository=$SourceRepository&channelId=$ChannelId&api-version=$MaestroApiVersion"

  $result = try { Invoke-WebRequest -Method Get -Uri $apiEndpoint -Headers $apiHeaders | ConvertFrom-Json } catch { Write-Host "Error: $_" }
  return $result
}

function Trigger-Subscription([string]$SubscriptionId) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroApiEndPoint/api/subscriptions/$SubscriptionId/trigger?api-version=$MaestroApiVersion"
  Invoke-WebRequest -Uri $apiEndpoint -Headers $apiHeaders -Method Post | Out-Null
}

function Assign-BuildToChannel([int]$BuildId, [int]$ChannelId) {
  $apiHeaders = Create-MaestroApiRequestHeaders -AuthToken $MaestroAccessToken
  $apiEndpoint = "$MaestroApiEndPoint/api/channels/${ChannelId}/builds/${BuildId}?api-version=$MaestroApiVersion"
  Invoke-WebRequest -Method Post -Uri $apiEndpoint -Headers $apiHeaders | Out-Null
}
