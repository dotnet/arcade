[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration,
  [string] $task,
  [string] $verbosity,
  [string] $msbuildEngine,
  [switch] $noRestore,
  [switch] $prepareMachine,
  [switch][Alias('nobl')]$excludeCIBinaryLog,
  [switch]$noWarnAsError,
  [switch] $help,
  [string] $runtimeSourceFeed,
  [string] $runtimeSourceFeedKey,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

$ci = $true
$binaryLog = if ($excludeCIBinaryLog) { $false } else { $true }
$warnAsError = if ($noWarnAsError) { $false } else { $true }
$restore = -not $noRestore

# Opt in to letting tools.ps1 own the CI/environment-aware defaults for the parameters it
# manages (e.g. configuration, verbosity). The $binaryLog/$warnAsError values set above by
# assignment are preserved, because only declared-but-unpassed parameters are unbound.
$importerBoundParameters = $PSBoundParameters

. $PSScriptRoot\tools.ps1

function Print-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -task <value>           Name of Arcade task (name of a project in toolset directory of the Arcade SDK package)"
  Write-Host "  -noRestore              Skip restoring dependencies"
  Write-Host "  -verbosity <value>      Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic]"
  Write-Host "  -help                   Print help and exit"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -prepareMachine         Prepare machine for CI run"
  Write-Host "  -msbuildEngine <value>  Msbuild engine to use to run build ('dotnet', 'vs', or unspecified)."
  Write-Host "  -excludeCIBinaryLog     When running on CI, allow no binary log (short: -nobl)"
  Write-Host ""
  Write-Host "Command line arguments not listed above are passed thru to msbuild."
}

try {
  if ($help -or (($null -ne $properties) -and ($properties.Contains('/help') -or $properties.Contains('/?')))) {
    Print-Usage
    exit 0
  }

  if ($task -eq "") {
    Write-PipelineTelemetryError -Category 'Build' -Message "Missing required parameter '-task <value>'"
    Print-Usage
    ExitWithExitCode 1
  }

  if( $msbuildEngine -eq "vs") {
    # Ensure desktop MSBuild is available for sdk tasks.
    $global:_MSBuildExe = InitializeVisualStudioMSBuild
  }

  $taskProject = GetSdkTaskProject $task
  if (!(Test-Path $taskProject)) {
    Write-PipelineTelemetryError -Category 'Build' -Message "Unknown task: $task"
    ExitWithExitCode 1
  }

  $log = Join-Path $LogDir "$task.binlog"
  $binaryLogArg = if ($binaryLog) { "/bl:$log" } else { "" }
  $restoreArg = if ($restore) { "/restore" } else { "" }
  $outputPath = Join-Path $ToolsetDir "$task\"

  MSBuild $taskProject `
    $binaryLogArg `
    $restoreArg `
    /t:Execute `
    /p:Configuration=$configuration `
    /p:RepoRoot=$RepoRoot `
    /p:BaseIntermediateOutputPath=$outputPath `
    /v:$verbosity `
    @properties
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'Build' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
