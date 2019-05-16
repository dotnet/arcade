[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $verbosity = "minimal",
  [string] $architecture = "",
  [string] $version = "Latest",
  [string] $runtime = "dotnet",
  [string] $installdir = ""
)

. $PSScriptRoot\tools.ps1

try {
  $dotnetRoot = Join-Path $RepoRoot ".dotnet"
  
  $dotnetInstallLocation = $installdir
  if (-not $dotnetInstallLocation) {
    $dotnetInstallLocation = $dotnetRoot
  }

  InstallDotNet $dotnetInstallLocation $version $architecture $runtime $true
} 
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}

ExitWithExitCode 0
