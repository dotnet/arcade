Param(
  [string] $barToken,
  [string] $gitHubPat,
  [string] $configuration = "Debug"
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

try {
  Write-Host "STEP 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  
  $validateSdkDir = "$ArtifactsDir\validatesdk\\"
  $packagesSource = "$validateSdkDir\packages\$configuration\NonShipping"  
  $toolsProjPath = "$RepoRoot\src\Microsoft.DotNet.Arcade.Sdk\tools\Tools.proj"
  $nugetConfigPath = "$RepoRoot\NuGet.config"
  
  # When restoring, we check if local sources defined in Tools.proj actually exist so we need to create
  # the validation SDK folder beforehand
  if (!(Test-Path -Path $packagesSource)) {
    Create-Directory $packagesSource
  }
  
  . .\common\build.ps1 -restore -build -pack -configuration $configuration -logFileName "Build_Local.binlog" /p:ArtifactsDir=$validateSdkDir
    
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

  . .\common\build.ps1 -configuration $configuration @Args  /p:AdditionalRestoreSources=$packagesSource
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
