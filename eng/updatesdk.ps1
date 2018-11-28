[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $BarToken,
  [string] $GitHubPat
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
  $packagesSource = "$PSScriptRoot\..\artifacts\packages\debug\NonShipping"
  . .\common\build.ps1 -restore -build -pack
  Check-ExitCode $lastExitCode
  
  Write-Host "STEP 2: Build using the local packages"

  $packagesSource = Resolve-Path $packagesSource
  
  Write-Host "Downloading nuget.exe"
  $nugetTempFolder = "$PSScriptRoot\..\nuget"
  $nugetExe = "$nugetTempFolder\nuget.exe"
  mkdir $nugetTempFolder
  Invoke-WebRequest -Uri https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $nugetExe
  
  Write-Host "Adding local nuget source..."
  & $nugetExe sources add -Name arcade-local -Source $packagesSource
  
  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  
  # We need to run darc in a different window since if it was just install 'darc' won't be recognized as a command
  Write-Host "Running `darc update-dependencies` in a different window..."
  start powershell { darc update-dependencies --packages-folder $packagesSource --password $BarToken --github-pat $GitHubPat }
  
  Check-ExitCode $lastExitCode
  Stop-Process -Name "dotnet"
  
  Write-Host "Building with updated dependencies"

  . $PSScriptRoot\common\build.ps1 -configuration $Configuration -restore -build -test -sign -restoreSources $packagesSource
  Check-ExitCode $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
finally {
  Write-Host "Cleaning up workspace for official build..."
  & $nugetExe sources remove -Name arcade-local
  git checkout -- Version.Details.xml
  git checkout -- Versions.props
  git checkout ../global.json
  stop-process -Name "dotnet"
  Remove-Item -Path $packagesSource -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
  Remove-Item -Path $nugetTempFolder -Recurse -Force -ErrorAction SilentlyContinue | Out-Null
  Pop-Location
  Write-Host "Finished building Arcade SDK with updated packages"
}