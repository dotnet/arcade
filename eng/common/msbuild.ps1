[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $verbosity = "minimal",
  [bool] $warnaserror = $true,
  [bool] $nodereuse = $true,
  [switch] $ci,
  [switch] $prepareMachine,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

. $PSScriptRoot\init-tools.ps1

try {
  $buildDriver, $buildArgs = GetBuildCommand

  $buildlog = Join-Path $LogDir "Build.binlog"

  $warnaserrorflag = if ($warnaserror) { "/warnaserror" }
  $nodereuseflag = if ($nodereuse) { "/nr:true" } else { "/nr:false" }

  & $buildDriver $buildArgs `
    /m /nologo /clp:Summary `
    /v:$verbosity `
    /bl:$buildlog `
    $warnaserrorflag `
    $nodereuseflag `
    @properties

  if ($lastExitCode -ne 0) {
    Write-Host "Build failed see log: $buildLog" -ForegroundColor DarkGray
  }
  ExitWithExitCode $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}