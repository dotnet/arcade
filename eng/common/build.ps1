[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $projects = "",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $deployDeps,
  [switch] $build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch] $test,
  [switch] $integrationTest,
  [switch] $sign,
  [switch] $pack,
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
  Write-Host "  -deploy                 Deploy built VSIXes"
  Write-Host "  -deployDeps             Deploy dependencies (Roslyn VSIXes for integration tests)"
  Write-Host "  -test                   Run all unit tests in the solution"
  Write-Host "  -integrationTest        Run all integration tests in the solution"
  Write-Host "  -sign                   Sign build outputs"
  Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
  Write-Host ""

  Write-Host "Advanced settings:"
  Write-Host "  -projects <value>       Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
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

# This is a temporary workaround for https://github.com/Microsoft/msbuild/issues/2095 and
# https://github.com/dotnet/cli/issues/6589
# Currently, SDK's always get resolved to the global location, but we want our packages
# to all be installed into a local folder (prevent machine contamination from global state).
# 
# We are restoring all of our packages locally and setting NuGetPackageRoot to reference the
# local location, but this breaks Custom SDK's which are expecting the SDK to be available
# from the global user folder.
function MakeGlobalSdkAvailableLocal {
  $RepoToolsetSource = Join-Path $DefaultNuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\"
  $RepoToolsetDestination = Join-Path $NuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\"
  if (!(Test-Path $RepoToolsetDestination)) {
    Copy-Item $RepoToolsetSource -Destination $RepoToolsetDestination -Recurse
  }
}

function InstallNativeTools {
  $NativeToolsInstaller = Join-Path $PSScriptRoot "init-tools-native.ps1"

  $NativeTools = $GlobalJson | Select-Object -Expand "native-tools" -ErrorAction SilentlyContinue
  if ($NativeTools) {
    & $NativeToolsInstaller
  }
}
function InstallToolset {
  if (!(Test-Path $ToolsetBuildProj)) {
    CreateDirectory $TempDir

    $proj = Join-Path $TempDir "_restore.proj"
    '<Project Sdk="RoslynTools.RepoToolset"><Target Name="NoOp"/></Project>' | Set-Content $proj
    & $DotNetExe msbuild $proj /t:NoOp /m /nologo /clp:None /warnaserror /v:$verbosity /p:NuGetPackageRoot=$NuGetPackageRoot /p:__ExcludeSdkImports=true
  }
}

function Build {
  if ($OfficialBuild) {
    MakeGlobalSdkAvailableLocal
  }

  if ($ci -or $log) {
    CreateDirectory($logDir)
    $logCmd = "/bl:" + (Join-Path $LogDir "Build.binlog")
  } else {
    $logCmd = ""
  }

  & $DotNetExe msbuild $ToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:RepoRoot=$RepoRoot /p:Projects=$projects /p:Restore=$restore /p:DeployDeps=$deployDeps /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:IntegrationTest=$integrationTest /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci /p:RestorePackagesPath=$NuGetPackageRoot /p:NuGetPackageRoot=$NuGetPackageRoot $properties
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

try {
  $RepoRoot = Join-Path $PSScriptRoot "..\..\"
  $DotNetRoot = Join-Path $RepoRoot ".\.dotnet"
  $DotNetExe = Join-Path $DotNetRoot "dotnet.exe"
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
  $TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"
  $GlobalJson = Get-Content(Join-Path $RepoRoot "global.json") -Raw | ConvertFrom-Json
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "true"
  $OfficialBuild = $false

  if ("$env:OfficialBuildId" -ne "") {
    $OfficialBuild = $true
  }

  if ($projects -eq "") {
    $projects = Join-Path $RepoRoot "**\*.sln"
  }

  if ($env:NUGET_PACKAGES -ne $null) {
    $NuGetPackageRoot = $env:NUGET_PACKAGES.TrimEnd("\") + "\"
    $DefaultNuGetPackageRoot = $NuGetPackageRoot
  } else {
    if ($OfficialBuild) {
      $NuGetPackageRoot = Join-Path $RepoRoot "packages\"
    } else {
      $NuGetPackageRoot = Join-Path $env:UserProfile ".nuget\packages\"
    }
    $DefaultNuGetPackageRoot = Join-Path $env:UserProfile ".nuget\packages\"
  }
  $ToolsetVersion = $GlobalJson.'msbuild-sdks'.'RoslynTools.RepoToolset'
  $ToolsetBuildProj = Join-Path $NuGetPackageRoot "roslyntools.repotoolset\$ToolsetVersion\tools\Build.proj"

  if ($ci) {
    CreateDirectory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  if ($restore) {
    InstallNativeTools
    InstallDotNetCli
    InstallToolset
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

