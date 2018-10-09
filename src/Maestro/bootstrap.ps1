if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
  Write-Warning "Script must be run in Admin Mode!"
  exit 1
}

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

& "$PSScriptRoot\MaestroApplication\bootstrap-certs.ps1"
if (-not $?) {
  Write-Error "Failed to bootstrap certs"
  exit $?
}
& "$PSScriptRoot\MaestroApplication\setup-localdb.ps1"
if (-not $?) {
  Write-Error "Failed to set up local db"
  exit $?
}
