# Pre-flight validation for the feed-publish WIF service connection used by the Maestro Build
# Promotion Pipeline (eng/publishing/v3/publish.yml -> GetFeedToken step).
#
# The Build Promotion Pipeline runs in BOTH the dnceng and devdiv AzDO organizations, but arcade's
# own promotion validation (eng/validation/validate-promotion.ps1) only ever exercises the dnceng
# promotion path. That blind spot is exactly what let a broken migration ship: the feed-publish
# service connection existed in dnceng but was missing in devdiv, so devdiv promotions failed with
# "service connection could not be found" while dnceng validation stayed green.
#
# This script closes that gap cheaply: for every org the promotion pipeline runs in, it asserts the
# feed-publish service connection exists and is Ready. If it is missing or not ready in ANY org, the
# validation fails, so the change is not promoted to the release channel while still broken.
#
# NOTE: The identity behind $AzdoToken must have service-endpoint read on each org/project listed in
# $Organizations. A dnceng-enrolled identity may not be able to read devdiv endpoints; if so this
# step surfaces the permission gap explicitly rather than silently skipping devdiv.

Param(
  [Parameter(Mandatory=$true)][string] $AzdoToken,   # AzDO OAuth/AAD access token (WIF), not a PAT; needs service-endpoint read.
  [string] $EndpointName = 'dnceng-artifact-feeds-publish',
  # Orgs/projects where the Build Promotion Pipeline runs and therefore where the SC must exist.
  # Format: 'org/project'. Keep in sync with eng/promote-build.yml consumers.
  [string[]] $Organizations = @('dnceng/internal', 'devdiv/DevDiv')
)

set-strictmode -version 2.0
$ErrorActionPreference = 'Stop'

$ci = $true
$disableConfigureToolsetImport = $true
. $PSScriptRoot\..\common\tools.ps1

$headers = @{ Authorization = "Bearer $AzdoToken" }
$failures = @()

$encodedEndpointName = [uri]::EscapeDataString($EndpointName)
foreach ($orgProject in $Organizations) {
  $uri = "https://dev.azure.com/$orgProject/_apis/serviceendpoint/endpoints?endpointNames=$encodedEndpointName&api-version=7.1-preview.4"
  Write-Host "Checking service connection '$EndpointName' in '$orgProject'..."
  try {
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
  }
  catch {
    $status = $null
    if ($_.Exception.PSObject.Properties['Response'] -and $_.Exception.Response) {
      $status = [int]$_.Exception.Response.StatusCode
    }
    if ($status -eq 401 -or $status -eq 403) {
      $failures += "[$orgProject] Not authorized (HTTP $status) to read service endpoints. The validation identity needs service-endpoint read in this org; without it the feed-publish connection cannot be verified here."
    }
    else {
      $failures += "[$orgProject] Failed to query service endpoints: $($_.Exception.Message)"
    }
    continue
  }

  $count = @($resp.value).Count
  if ($count -eq 0) {
    $failures += "[$orgProject] Service connection '$EndpointName' does NOT exist. The Build Promotion Pipeline runs in this org, so the connection (with a matching federated identity credential on the backing app registration) must be created here."
    continue
  }

  $endpoint = @($resp.value)[0]
  $isReady = $false
  $readyProp = $endpoint.PSObject.Properties['isReady']
  if ($readyProp) { $isReady = [bool]$readyProp.Value }
  if (-not $isReady) {
    $failures += "[$orgProject] Service connection '$EndpointName' (id $($endpoint.id)) exists but is NOT ready."
    continue
  }

  Write-Host "  OK: '$EndpointName' exists and is ready (id $($endpoint.id))."
}

if ($failures.Count -gt 0) {
  $message = "Feed-publish service connection validation failed:`n - " + ($failures -join "`n - ")
  Write-PipelineTelemetryError -Category 'ValidateFeedPublishConnections' -Message $message
  ExitWithExitCode 1
}

Write-Host "All feed-publish service connections are present and ready across: $($Organizations -join ', ')."
ExitWithExitCode 0
