param (
    $buildUrl,
    $azdoPat,
    $appInsightsAppId,
    $appInsightsKey
)

$base64authinfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$azdoPat"))
$vstsAuthHeader = @{"Authorization"="Basic $base64authinfo"}
$allHeaders = $vstsAuthHeader + @{"Content-Type"="application/json"; "Accept"="application/json"}

# Find the plan info

$buildUri = [System.Uri]$buildUrl
$buildId = $null
$account = $null
$project = $null

if ($buildUri.Host -eq "dev.azure.com") {
    $account = $buildUri.Segments[1].Replace("/", "")
    $project = $buildUri.Segments[2].Replace("/", "")
}

if ($buildUri.Query -match "buildId=(?<buildId>\d+)") {
    $buildId = [int]$Matches.buildId
} else {
    throw "Could not parse build url $buildUrl"
}

Write-Verbose "Looking up plan info for build $buildId in project $project on account $account"

$buildInfo = Invoke-WebRequest -Method Get -Headers $allHeaders -Uri "https://dev.azure.com/$account/$project/_apis/build/builds/$buildId" | ConvertFrom-Json
$planId = $buildInfo.orchestrationPlan.planId

Write-Verbose "Found plan id $planId, looking up in appinsights"

$appInsightsHeaders = @{ “X-Api-Key” = $appInsightsKey; “Content-Type” = “application/json” }
$appInsightsOperation = "query"
$appInsightsQuery = [uri]::EscapeUriString("?query=traces|where message contains `"Successfully submitted new Helix job`" | where customDimensions.orchestrationId contains `"$planId`"")
$fullUri = “https://api.applicationinsights.io/v1/apps/$appInsightsAppId/$appInsightsOperation$appInsightsQuery”
$appInsightsData = Invoke-WebRequest -Method Get -Uri $fullUri -Headers $appInsightsHeaders | ConvertFrom-Json

Write-Host ""
$firstRow = $true
foreach ($row in $appInsightsData.tables.rows) {
    $customDims = ConvertFrom-Json $row[4]
    if ($firstRow) {
        $firstRow = $false
        Write-Host "Pool: $($customDims.agentPool)"
        Write-Host "Plan Id: $($customDims.orchestrationId)"
        Write-Host ""
    }
    Write-Host "Agent Id:                      $($customDims.agentId)"
    Write-Host "Job Name:                      $($customDims.jobName)"
    Write-Host "Queue:                         $($customDims.queueId)"
    Write-Host "Helix Correlation Id:          $($customDims.helixJob)"
    Write-Host "Helix Work Item Friendly Name: $($customDims.workItemName)"
    Write-Host ""
}