Param(
  [string] $barToken,
  [string] $gitHubPat,
  [string] $configuration = "Debug",
  [switch] $validateSdk
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot\common\tools.ps1
$LocalNugetConfigSourceName = "arcade-local"

function Check-ExitCode ($exitCode)
{
  if ($exitCode -ne 0) {
    Write-Host "Arcade self-build failed"
    ExitWithExitCode $exitCode
  }
}

function StopDotnetIfRunning
{
    $dotnet = Get-Process "dotnet" -ErrorAction SilentlyContinue
    if ($dotnet) {
        stop-process $dotnet
    }
}

function AddSourceToNugetConfig([string]$nugetConfigPath, [string]$source) 
{
    Write-Host "Adding '$source' to '$nugetConfigPath'..."
    
    $nugetConfig = New-Object XML
    $nugetConfig.PreserveWhitespace = $true
    $nugetConfig.Load($nugetConfigPath)
    $packageSources = $nugetConfig.SelectSingleNode("//packageSources")
    $keyAttribute = $nugetConfig.CreateAttribute("key")
    $keyAttribute.Value = $LocalNugetConfigSourceName
    $valueAttribute = $nugetConfig.CreateAttribute("value")
    $valueAttribute.Value = $source
    $newSource = $nugetConfig.CreateElement("add")
    $newSource.Attributes.Append($keyAttribute)
    $newSource.Attributes.Append($valueAttribute)
    $packageSources.AppendChild($newSource)
    $nugetConfig.Save($nugetConfigPath)
}

function AddSourceToToolsProj([string]$toolsProjPath, [string]$source) 
{
    Write-Host "Adding '$source' to '$toolsProjPath'..."
    
    $toolsProj = New-Object XML
    $toolsProj.Load($toolsProjPath)
    $restoreSources = $toolsProj.SelectSingleNode("//RestoreSources[not(@*)]")
    $restoreSources.InnerText = $restoreSources.InnerText + "$source;"
    $toolsProj.Save($toolsProjPath)
}

try {
  Write-Host "STEP 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  
  $validateSdkDir = "$PSScriptRoot\..\artifacts\validatesdk\"
  $packagesSource = "$validateSdkDir\packages\$configuration\NonShipping"  
  $toolsProjPath = "$PSScriptRoot\..\src\Microsoft.DotNet.Arcade.Sdk\tools\Tools.proj"
  $nugetConfigPath = "$PSScriptRoot\..\NuGet.config"
  
  # When restoring, we check if local sources defined in Tools.proj actually exist so we need to create
  # the validation SDK folder beforehand
  if (!(Test-Path -Path $packagesSource)) {
    New-Item $packagesSource -ItemType Directory
  }
  
  # Adding a source by using /p:RestoreSources sets the system to just use that source, but if this source has packages
  # which depend in different sources, the restore process fails since the dependencies are not found. Workaround is to
  # append the new source to the existing collection of sources
  AddSourceToToolsProj $toolsProjPath $packagesSource
  
  . .\common\build.ps1 -restore -build -pack -configuration $configuration -logFileName "Build_Local.binlog" /p:ArtifactsDir=$validateSdkDir
   Check-ExitCode $lastExitCode
  
  Write-Host "STEP 2: Build using the local packages"
  
  AddSourceToNugetConfig $nugetConfigPath $packagesSource
   
  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  
  $DarcExe = "$env:USERPROFILE\.dotnet\tools"
  $DarcExe = Resolve-Path $DarcExe

  & $DarcExe\darc.exe update-dependencies --packages-folder $packagesSource --password $barToken --github-pat $gitHubPat --channel ".NET Tools - Latest"
  
  Check-ExitCode $lastExitCode
  StopDotnetIfRunning
  
  Write-Host "Building with updated dependencies"

  . .\common\build.ps1 -configuration $configuration @Args
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
  StopDotnetIfRunning
  Pop-Location
  Write-Host "Finished building Arcade SDK with validation enabled!"
}