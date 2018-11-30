[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $BarToken,
  [string] $GitHubPat,
  [string] $PackageSource,
  [string] $Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot\common\tools.ps1

function Check-ExitCode ($exitCode)
{
  if ($exitCode -ne 0) {
    Write-Host "Arcade self-build failed"
    ExitWithExitCode $exitCode
  }
}

try {
  Write-Host "Starting Arcade SDK Package Update"
  
  Write-Host "STEP 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  . .\common\build.ps1 -restore -build -pack -configuration $Configuration -logFileName "Build_Step1.binlog"
  Check-ExitCode $lastExitCode
  
  Write-Host "STEP 2: Build using the local packages"

  $PackageSource = Resolve-Path $PackageSource
  
  Write-Host "Downloading nuget.exe"
  $nugetTempFolder = "$PSScriptRoot\..\nuget"
  $nugetExe = "$nugetTempFolder\nuget.exe"
  mkdir $nugetTempFolder
  Invoke-WebRequest -Uri https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nugetExe
  
  Write-Host "Adding local nuget source..."
  & $nugetExe sources add -Name arcade-local -Source $PackageSource
  
  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  
  $DarcExe = "$env:USERPROFILE\.dotnet\tools"
  $DarcExe = Resolve-Path $DarcExe
  & $DarcExe\darc.exe update-dependencies --packages-folder $PackageSource --password $BarToken --github-pat $GitHubPat
  
  Check-ExitCode $lastExitCode
  Stop-Process -Name "dotnet"
  
  Write-Host "Building with updated dependencies"

  . .\common\build.ps1 -restore -build -pack -test -sign -configuration $Configuration -logFileName "Build_Step2.binlog" /p:RestoreSources=$PackageSource
  Check-ExitCode $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
finally {
  Write-Host "Cleaning up workspace..."
  & $nugetExe sources remove -Name arcade-local
  stop-process -Name "dotnet"
  Remove-Item -Path $nugetTempFolder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
  Pop-Location
  Write-Host "Finished building Arcade SDK with updated packages"
}