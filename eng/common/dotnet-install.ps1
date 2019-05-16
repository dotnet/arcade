[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $verbosity = "minimal",
  [string] $architecture = "",
  [string] $version = "Latest",
  [string] $runtime = "dotnet"
)

. $PSScriptRoot\tools.ps1

try {
  $dotnetRoot = Join-Path $RepoRoot ".dotnet"
  
  $dotnetInstallLocation = $dotnetRoot
  if ($architecture -and -not ($architecture -like '')) {
    $dotnetInstallLocation = Join-Path $dotnetRoot $architecture
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
