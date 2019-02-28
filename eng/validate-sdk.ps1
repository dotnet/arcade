Param(
  [string] $barToken,
  [string] $gitHubPat,
  [string] $configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot\common\tools.ps1
$LocalNugetConfigSourceName = "arcade-local"

function CheckExitCode ([string]$stage)
{
  $exitCode = $LASTEXITCODE
  if ($exitCode  -ne 0) {
    Write-Host "Something failed in stage: '$stage'. Check for errors above. Exiting now..."
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
    $newSource.Attributes.Append($keyAttribute) | Out-Null
    $newSource.Attributes.Append($valueAttribute) | Out-Null
    $packageSources.AppendChild($newSource) | Out-Null
    $nugetConfig.Save($nugetConfigPath)
}

function MoveArtifactsToValidateSdkFolder([string]$artifactsDir, [string]$validateSdkFolderName, [string]$repoRoot)
{
    Rename-Item -Path $artifactsDir -NewName $validateSdkFolderName -Force
  
    if (!(Test-Path -Path $artifactsDir)) {
        Create-Directory $artifactsDir
    }
  
    Move-Item -Path (Join-Path $repoRoot $validateSdkFolderName) -Destination $artifactsDir -Force
}
function IntegrateBinLogs([string]$artifactsDir, [string]$validateSdkFolderName, [string]$repoRoot, [string]$configuration)
{
    $artifactsLogDir = Join-Path (Join-Path $artifactsDir "log") $configuration
    $validateSdkLogDir = Join-Path (Join-Path(Join-Path $artifactsDir $validateSdkFolderName) "log") $configuration
    
    Write-Host "Moving binlog files from '$validateSdkLogDir' to '$artifactsLogDir'"
    
    # If there is no log folder in artifacts either there were no binlogs captured or something else failed previously
    # so we just exit with exit code 0 to avoid failing the whole build.
    if (!(Test-Path -Path $artifactsLogDir)) {
        Write-Host "There is no log folder in '$artifactsDir'..."
        ExitWithExitCode 0
    }
    
    if ((Test-Path -Path $validateSdkLogDir)) {
        Get-ChildItem $validateSdkLogDir |
        Foreach-Object {
            $newFileName = "Validation_$_"
            Rename-Item -Path $_.FullName -NewName $newFileName -Force
            Copy-Item -Path $_.FullName.replace($_, $newFileName) -Destination $artifactsLogDir -Force
        }
    }
}

try {
  Write-Host "STEP 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  
  $validateSdkFolderName = "validatesdk"
  $validateSdkDir = Join-Path $ArtifactsDir $validateSdkFolderName
  $packagesSource = Join-Path (Join-Path (Join-Path $validateSdkDir "packages") $configuration) "NonShipping"
  $nugetConfigPath = Join-Path $RepoRoot "NuGet.config"
  
  & .\common\cibuild.cmd -configuration $configuration @Args
  CheckExitCode "Local build"

  # This is a temporary solution. When https://github.com/dotnet/arcade/issues/1293 is closed
  # we'll be able to pass a container name to build.ps1 which will put the outputs in the
  # artifacts-<container-name> folder.
  MoveArtifactsToValidateSdkFolder $ArtifactsDir $validateSdkFolderName $RepoRoot
  CheckExitCode "Move outputs to validatesdk folder"

  Write-Host "STEP 2: Build using the local packages"
  
  AddSourceToNugetConfig $nugetConfigPath $packagesSource
  CheckExitCode "Adding source to NuGet.config"

  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  CheckExitCode "Running darc-init"

  $DarcExe = "$env:USERPROFILE\.dotnet\tools"
  $DarcExe = Resolve-Path $DarcExe

  & $DarcExe\darc.exe update-dependencies --packages-folder $packagesSource --password $barToken --github-pat $gitHubPat --channel ".NET Tools - Latest"
  CheckExitCode "Updating dependencies"
  StopDotnetIfRunning
  
  Write-Host "Building with updated dependencies"

  & .\common\cibuild.cmd -configuration $configuration @Args /p:AdditionalRestoreSources=$packagesSource /p:DotNetPublishBlobFeedUrl=https://dotnetfeed.blob.core.windows.net/dotnet-core-test/index.json
  CheckExitCode "Official build"
  Write-Host "Finished building Arcade SDK with validation enabled!"
  
  IntegrateBinLogs $artifactsDir $validateSdkFolderName $RepoRoot $configuration
  CheckExitCode "Binlog Integration"
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
}
