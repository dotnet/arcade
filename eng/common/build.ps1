[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $build,
  [switch] $rebuild,
  [switch] $test,
  [switch] $sign,
  [switch] $pack,
  [switch] $publish,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $log,
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

function Print-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -configuration <value>  Build configuration Debug, Release"
  Write-Host "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
  Write-Host "  -help                   Print help and exit"
  Write-Host ""

  Write-Host "Actions:"
  Write-Host "  -restore                Restore dependencies"
  Write-Host "  -build                  Build solution"
  Write-Host "  -rebuild                Rebuild solution"
  Write-Host "  -test                   Run all unit tests in the solution"
  Write-Host "  -sign                   Sign build outputs"
  Write-Host "  -pack                   Package build outputs into NuGet packages"
  Write-Host "  -publish                Publish built packages"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -ci                     Set when running on CI server"
  Write-Host "  -log                    Enable logging (by default on CI)"
  Write-Host "  -prepareMachine         Prepare machine for CI run"
  Write-Host ""
  Write-Host "Command line arguments not listed above are passed thru to msbuild."
  Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

function CreateDirectory([string[]] $path) {
  if (!(Test-Path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function InstallDotNetCli {
  $installScript = "$DotNetRoot\dotnet-install.ps1"
  if (!(Test-Path $installScript)) { 
    CreateDirectory $DotNetRoot
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
  }
  
  & $installScript -Version $GlobalJson.sdk.version -InstallDir $DotNetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet cli (exit code '$lastExitCode')."
  }
}
function InstallNativeTools {
  $NativeToolsInstaller = Join-Path $PSScriptRoot "init-tools-native.ps1"

  $NativeTools = $GlobalJson | Select-Object -Expand "native-tools" -ErrorAction SilentlyContinue
  if ($NativeTools) {
    & $NativeToolsInstaller
  }
}

function Build {

  $msbuildArgs = @(
    (Join-Path $RepoRoot build.proj),
    '/m',
    '/nologo',
    '/clp:Summary',
    '/warnaserror',
    "/v:$verbosity",
    "/p:Configuration=$configuration",
    "/p:CIBuild=$ci"
  )

  if ($ci -or $log) {
    CreateDirectory($logDir)
    $msbuildArgs += "/bl:$(Join-Path $LogDir Build.binlog)"
  }

  $targets = @()

  if ($rebuild) {
    $targets += "Rebuild"
  } elseif ($build) {
    $targets += "Build"
  }

  if ($test) {
    $targets += "Test"
  }

  if ($pack) {
    $targets += "Pack"
  }

  if ($sign) {
    $targets += "Sign"
  }

  if ($publish) {
    $targets += "Publish"
  }

  $targets = $targets -join ';'

  if ($restore) {
    if ($targets) {
      $msbuildArgs += "/restore"
    } else {
      $targets = "Restore"
    }
  }

  & $DotNetExe msbuild "/t:$targets" @msbuildArgs @properties
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

try {
  $RepoRoot = [System.IO.Path]::GetfullPath((Join-Path $PSScriptRoot "..\..\"))
  $DotNetRoot = Join-Path $RepoRoot ".dotnet"
  $DotNetExe = Join-Path $DotNetRoot "dotnet.exe"
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $LogDir = Join-Path $ArtifactsDir "log"
  $TempDir = Join-Path $ArtifactsDir "tmp"
  $GlobalJson = Get-Content(Join-Path $RepoRoot "global.json") -Raw | ConvertFrom-Json
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "true"
  $OfficialBuild = $false

  if ("$env:OfficialBuildId" -ne "") {
    $OfficialBuild = $true
  }

  if ($ci) {
    CreateDirectory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  if ($restore) {
    InstallNativeTools
    InstallDotNetCli
  }

  Build
  exit $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($ci -and $prepareMachine) {
    Stop-Processes
  }
}

