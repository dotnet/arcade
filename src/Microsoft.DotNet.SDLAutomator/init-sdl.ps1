Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $DncEngAccessToken,
  [string] $GdnLoggerLevel="Standard"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$encodedPat = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes(":$DncEngAccessToken"))
$uri = "https://dev.azure.com/dnceng/internal/_apis/git/repositories/sdl-tool-cfg/Items?path=$([Uri]::EscapeDataString("/$Repository/.gdn"))&versionDescriptor[versionOptions]=0&`$format=zip&api-version=5.0-preview.1"
$zipFile = "$SourcesDirectory/gdn.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem
Try
{
  Write-Host "Downloading gdn folder from internal config repostiory..."
  Invoke-WebRequest -Headers @{ "Accept"="application/zip"; "Authorization"="Basic $encodedPat" } -Uri $uri -OutFile $zipFile
  [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $sourcesDirectory)
} Catch [System.Net.WebException] {
  # if the folder does not exist, we'll do a guardian init and push it to the remote repository
  Write-Host "Initializing Guardian..."
  Write-Host "guardian init --working-directory $SourcesDirectory --logger-level $GdnLoggerLevel"
  guardian init --working-directory $SourcesDirectory --logger-level $GdnLoggerLevel
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian init failed with exit code $LASTEXITCODE."
  }
  Write-Host "guardian baseline --working-directory $SourcesDirectory --name mainbaseline"
  guardian baseline --working-directory $SourcesDirectory --name mainbaseline
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian baseline failed with exit code $LASTEXITCODE."
  }
  Invoke-Expression "$PSScriptRoot/push-gdn.ps1 -Repository $Repository -SourcesDirectory $SourcesDirectory -GdnFolder $SourcesDirectory/.gdn -DncEngAccessToken $DncEngAccessToken -PushReason `"Initialize gdn folder`""
}