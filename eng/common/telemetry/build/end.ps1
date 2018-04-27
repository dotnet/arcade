[CmdletBinding()]
param(
  [int]$ErrorCount,
  [int]$WarningCount
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"


try {
  Invoke-RestMethod -Uri "https://helix.dot.net/api/2018-03-14/telemetry/job/build/$env:Helix_WorkItemId/finish?errorCount=$ErrorCount&warningCount=$WarningCount" -Method Post -ContentType "application/json" -Body "" `
    -Headers @{ 'X-Helix-Job-Token'=$env:Helix_JobToken }
}
catch {
  Write-Error $_
  Write-Error $_.Exception
  exit 1
}
