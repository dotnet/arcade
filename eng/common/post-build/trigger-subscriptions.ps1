param(
  [Parameter(Mandatory=$true)][string] $SourceRepo,
  [string] $MaestroEndpoint = "https://maestro-prod.westus2.cloudapp.azure.com",
  [string] $BarToken,
  [string] $ApiVersion = "2019-01-16"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

. $PSScriptRoot\..\tools.ps1

function Get-Headers([string]$accept, [string]$barToken) {
  $headers = New-Object 'System.Collections.Generic.Dictionary[[String],[String]]'
  $headers.Add('Accept',$accept)
  $headers.Add('Authorization',"Bearer $barToken")
  return $headers
}

# Get all the $SourceRepo subscriptions
$normalizedSurceRepo = $SourceRepo.Replace('dnceng@', '')
$getSubscriptionsApiEndpoint = "$maestroEndpoint/api/subscriptions?sourceRepository=$SourceRepo&api-version=$apiVersion"
$headers = Get-Headers 'application/json' $barToken

$subscriptions = Invoke-WebRequest -Uri $getSubscriptionsApiEndpoint -Headers $headers | ConvertFrom-Json

if (!$subscriptions) {
  Write-Host "No subscriptions found for source repo '$SourceRepo'"
  return
}

$subscriptionsToTrigger = New-Object System.Collections.Generic.List[string]

# Get all enabled subscriptions that need dependency flow on 'everyBuild'
foreach ($subscription in $subscriptions) {
  if ($subscription.enabled -and $subscription.policy.updateFrequency -like 'everyBuild') {
    Write-Host "$subscription.id"
    [void]$subscriptionsToTrigger.Add($subscription.id)
  }
}

foreach ($subscriptionToTrigger in $subscriptionsToTrigger) {
  try {
    $triggerSubscriptionApiEndpoint = "$maestroEndpoint/api/subscriptions/$subscriptionToTrigger/trigger?api-version=$apiVersion"
    $headers = Get-Headers 'application/json' $BarToken
    
    Write-Host "Triggering subscription '$subscriptionToTrigger'..."

    Invoke-WebRequest -Uri $triggerSubscriptionApiEndpoint -Headers $headers -Method Post
  
    Write-Host "Subscription '$subscriptionToTrigger' triggered!"
  } 
  catch
  {
    Write-Host "There was an error while triggering subscription '$subscriptionToTrigger'"
    Write-Host $_
    Write-Host $_.ScriptStackTrace
  }
}