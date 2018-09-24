# Initialize variables if they aren't already defined
$ci = if (Test-Path variable:ci) { $ci } else { $false }
$restore = if (Test-Path variable:restore) { $restore } else { $true }

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Create-Directory([string[]] $path) {
  if (!(Test-Path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function InitializeDotNetCli {
  # Don't resolve runtime, shared framework, or SDK from other locations to ensure build determinism
  $env:DOTNET_MULTILEVEL_LOOKUP=0

  # Disable first run since we do not need all ASP.NET packages restored.
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Source Build uses DotNetCoreSdkDir variable
  if ($env:DotNetCoreSdkDir -ne $null) {
    $env:DOTNET_INSTALL_DIR = $env:DotNetCoreSdkDir
  }

  # Use dotnet installation specified in DOTNET_INSTALL_DIR if it contains the required SDK version,
  # otherwise install the dotnet CLI and SDK to repo local .dotnet directory to avoid potential permission issues.
  if (($env:DOTNET_INSTALL_DIR -ne $null) -and (Test-Path(Join-Path $env:DOTNET_INSTALL_DIR "sdk\$($GlobalJson.tools.dotnet)"))) {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
  } else {
    $dotnetRoot = Join-Path $RepoRoot ".dotnet"
    $env:DOTNET_INSTALL_DIR = $dotnetRoot

    if ($restore) {
      InstallDotNetSdk $dotnetRoot $GlobalJson.tools.dotnet
    }
  }

  return $dotnetRoot
}

function GetDotNetInstallScript([string] $dotnetRoot) {
  $installScript = "$dotnetRoot\dotnet-install.ps1"
  if (!(Test-Path $installScript)) {
    Create-Directory $dotnetRoot
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
  }

  return $installScript
}

function InstallDotNetSdk([string] $dotnetRoot, [string] $version) {
  $installScript = GetDotNetInstallScript $dotnetRoot

  & $installScript -Version $version -InstallDir $dotnetRoot
  if ($lastExitCode -ne 0) {
    Write-Host "Failed to install dotnet cli (exit code '$lastExitCode')." -ForegroundColor Red
    exit $lastExitCode
  }
}

function InitializeVisualStudioBuild {
  $inVSEnvironment = !($env:VS150COMNTOOLS -eq $null) -and (Test-Path $env:VS150COMNTOOLS)

  if ($inVSEnvironment) {
    $vsInstallDir = Join-Path $env:VS150COMNTOOLS "..\.."
  } else {
    $vsInstallDir = LocateVisualStudio

    $env:VS150COMNTOOLS = Join-Path $vsInstallDir "Common7\Tools\"
    $env:VSSDK150Install = Join-Path $vsInstallDir "VSSDK\"
    $env:VSSDKInstall = Join-Path $vsInstallDir "VSSDK\"
  }

  return $vsInstallDir;
}

function LocateVisualStudio {
  $vswhereVersion = $GlobalJson.tools.vswhere
  $toolsRoot = Join-Path $RepoRoot ".tools"
  $vsWhereDir = Join-Path $toolsRoot "vswhere\$vswhereVersion"
  $vsWhereExe = Join-Path $vsWhereDir "vswhere.exe"

  if (!(Test-Path $vsWhereExe)) {
    Create-Directory $vsWhereDir
    Write-Host "Downloading vswhere"
    Invoke-WebRequest "https://github.com/Microsoft/vswhere/releases/download/$vswhereVersion/vswhere.exe" -OutFile $vswhereExe
  }

  $vsInstallDir = & $vsWhereExe -latest -prerelease -property installationPath -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VSSDK -requires Microsoft.Net.Component.4.6.TargetingPack -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -requires Microsoft.VisualStudio.Component.VSSDK

  if ($lastExitCode -ne 0) {
    Write-Host "Failed to locate Visual Studio (exit code '$lastExitCode')." -ForegroundColor Red
    exit $lastExitCode
  }

  return $vsInstallDir
}

function InitializeTools() {
  $tools = $GlobalJson.tools

  if ((Get-Member -InputObject $tools -Name "dotnet") -ne $null) {
    $dotnetRoot = InitializeDotNetCli

    # by default build with dotnet cli:
    $script:buildDriver = Join-Path $dotnetRoot "dotnet.exe"
    $script:buildArgs = "msbuild"
  }

  if ((Get-Member -InputObject $tools -Name "vswhere") -ne $null) {
    $vsInstallDir = InitializeVisualStudioBuild

    # Presence of vswhere.version indicates the repo needs to build using VS msbuild:
    $script:buildDriver = Join-Path $vsInstallDir "MSBuild\15.0\Bin\msbuild.exe"
    $script:buildArgs = "/nodeReuse:$(!$ci)"
  }

  if ($buildDriver -eq $null) {
    Write-Host "/global.json must either specify 'tools.dotnet' or 'tools.vswhere'." -ForegroundColor Red
    exit 1
  }

  InitializeToolSet $script:buildDriver $script:buildArgs
  InitializeCustomToolset
}

function GetBuildCommand() {
  if ($ci) {
    Write-Host "Using $buildDriver"
  }

  return $script:buildDriver, $script:buildArgs
}

function InitializeToolset([string] $buildDriver, [string]$buildArgs) {
  $toolsetVersion = $GlobalJson.'msbuild-sdks'.'Microsoft.DotNet.Arcade.Sdk'
  $toolsetLocationFile = Join-Path $ToolsetDir "$toolsetVersion.txt"

  if (Test-Path $toolsetLocationFile) {
    $path = Get-Content $toolsetLocationFile -TotalCount 1
    if (Test-Path $path) {
      $global:ToolsetBuildProj = $path
      return
    }
  }

  if (-not $restore) {
    Write-Host  "Toolset version $toolsetVersion has not been restored."
    exit 1
  }

  $proj = Join-Path $ToolsetDir "restore.proj"

  '<Project Sdk="Microsoft.DotNet.Arcade.Sdk"/>' | Set-Content $proj
  & $buildDriver $buildArgs $proj /t:__WriteToolsetLocation /m /nologo /clp:None /warnaserror /bl:$ToolsetRestoreLog /v:$verbosity /p:__ToolsetLocationOutputFile=$toolsetLocationFile

  if ($lastExitCode -ne 0) {
    Write-Host "Failed to restore toolset (exit code '$lastExitCode')." -ForegroundColor Red
    Write-Host "Build log: $ToolsetRestoreLog" -ForegroundColor DarkGray
    exit $lastExitCode
  }

  $path = Get-Content $toolsetLocationFile -TotalCount 1
  if (!(Test-Path $path)) {
    throw "Invalid toolset path: $path"
  }

  $global:ToolsetBuildProj = $path
}

function InitializeCustomToolset {
  if (-not $restore) {
    return
  }

  $script = Join-Path $EngRoot "RestoreToolset.ps1"

  if (Test-Path $script) {
    . $script
  }
}

try {
  $RepoRoot = Join-Path $PSScriptRoot "..\.."
  $EngRoot = Join-Path $PSScriptRoot ".."
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $ToolsetDir = Join-Path $ArtifactsDir "toolset"
  $LogDir = Join-Path $ArtifactsDir "log"
  $ToolsetRestoreLog = Join-Path $LogDir "ToolsetRestore.binlog"
  $TempDir = Join-Path $ArtifactsDir "tmp"
  $GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot "global.json") | ConvertFrom-Json

  if ($env:NUGET_PACKAGES -eq $null) {
    # Use local cache on CI to ensure deterministic build,
    # use global cache in dev builds to avoid cost of downloading packages.
    $env:NUGET_PACKAGES = if ($ci) { Join-Path $RepoRoot ".packages" }
                          else { Join-Path $env:UserProfile ".nuget\packages" }
  }

  Create-Directory $ToolsetDir
  Create-Directory $LogDir

  if ($ci) {
    Create-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }

  InitializeTools
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}

