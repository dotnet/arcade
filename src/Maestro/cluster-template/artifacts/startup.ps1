param(
  [Parameter(Mandatory = $True)]
  [string]
  $AppInsightsKey
)

setx APPLICATION_INSIGHTS_KEY "$AppInsightsKey"  /M

reg import (Join-Path $PSScriptRoot "update-ciphers.reg")
