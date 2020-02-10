Param(
  [string] $barToken,
  [string] $gitHubPat,
  [string] $configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot\common\tools.ps1
$LocalNugetConfigSourceName = "arcade-local"

# Batch and executable files exit and define $LASTEXITCODE.  Powershell commands exit and define $?
function CheckExitCode ([string]$stage, [bool]$commandExitCode = $True)
{
  if($commandExitCode -eq -$False) {
    $exitCode = 1
  }
  else {
    $exitCode = $LASTEXITCODE
  }

  if ($exitCode -ne 0) {
    Write-Host "Something failed in stage: '$stage'. Check for errors above. Exiting now with exit code $exitCode..."
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

function MoveFolderToSubFolder([string]$sourceDirectory, [string]$subFolderName)
{
    $parentDirectory = Split-Path -Path $sourceDirectory -Parent
    Rename-Item -Path $sourceDirectory -NewName $subFolderName -Force -Verbose
    if ($? -ne $True) {
        return $False
    }

    Create-Directory $sourceDirectory
    Move-Item -Path (Join-Path $parentDirectory $subFolderName) -Destination $sourceDirectory -Force -Verbose
    if ($? -ne $True) {
        return $False
    }
    return $True
}

try {
  Write-Host "Stage 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  
  $stage1ArtifactsFolderName = "artifacts_stage_1"
  $stage1SdkDir = Join-Path $RepoRoot $stage1ArtifactsFolderName
  $packagesSource = Join-Path (Join-Path (Join-Path $stage1SdkDir "packages") $configuration) "NonShipping"
  $nugetConfigPath = Join-Path $RepoRoot "NuGet.config"

  & .\common\cibuild.cmd -configuration $configuration @Args
  CheckExitCode "Local build"

  # This is a temporary solution. When https://github.com/dotnet/arcade/issues/1293 is closed
  # we'll be able to pass a container name to build.ps1 which will put the outputs in the
  # artifacts-<container-name> folder.
  Rename-Item -Path $ArtifactsDir -NewName $stage1ArtifactsFolderName -Verbose
  CheckExitCode "Preserve artifacts for stage 1 build" $?
  Write-Host "Stage 2: Build using the local packages"

  AddSourceToNugetConfig $nugetConfigPath $packagesSource
  CheckExitCode "Adding source to NuGet.config"

  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  CheckExitCode "Running darc-init"

  $DarcExe = "$env:USERPROFILE\.dotnet\tools"
  $DarcExe = Resolve-Path $DarcExe

  & $DarcExe\darc.exe update-dependencies --packages-folder $packagesSource --password $barToken --github-pat $gitHubPat --channel ".NET 3 Eng"
  CheckExitCode "Updating dependencies"
  StopDotnetIfRunning
  
  Write-Host "Building with updated dependencies"

  $ArtifactsLogDir = Join-Path (Join-Path $ArtifactsDir "log") $configuration
  & .\common\cibuild.cmd -configuration $configuration @Args /p:DotNetPublishBlobFeedUrl=https://dotnetfeed.blob.core.windows.net/dotnet-core-test/index.json
  CheckExitCode "Official build"

  StopDotnetIfRunning

  # Preserve build artifacts from stage 1 and stage 2
  # move logs to stage 2
  $exitCode = MoveFolderToSubFolder $ArtifactsLogDir "stage2"
  CheckExitCode "Move stage2 logs" $exitCode

  # copy logs from stage 1
  $stage1SourceLogDir = Join-Path (Join-Path $stage1SdkDir "log") $configuration
  $stage1TargetLogDir = Join-Path $ArtifactsLogDir "stage1"
  $stage1SourceAssetManifestDir = Join-Path $stage1SourceLogDir "AssetManifest"
  Create-Directory $stage1TargetLogDir
  Copy-Item -Path "$stage1SourceLogDir\*" -Destination $stage1TargetLogDir -Recurse -Force -Verbose
  CheckExitCode "Copy logs from stage 1" $?

  # copy manifest from stage 1
  $ArtifactsManifestDir = Join-Path $artifactsLogDir "AssetManifest"
  Create-Directory $ArtifactsManifestDir
  Copy-Item -Path "$stage1SourceAssetManifestDir\*" -Destination $ArtifactsManifestDir -Recurse -Force -Verbose
  CheckExitCode "Copy asset manifests from stage 1" $?

  Write-Host "Finished building Arcade SDK with validation enabled!"
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
