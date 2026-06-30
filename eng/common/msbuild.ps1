[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $verbosity,
  [bool] $warnAsError,
  [bool] $nodeReuse,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $excludePrereleaseVS,
  [string] $msbuildEngine,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$extraArgs
)

# Opt in to letting tools.ps1 own the CI/environment-aware defaults for the parameters it
# manages (e.g. nodeReuse, warnAsError, verbosity); see tools.ps1 for details.
$importerBoundParameters = $PSBoundParameters

. $PSScriptRoot\tools.ps1

try {
  MSBuild @extraArgs
} 
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Build' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0