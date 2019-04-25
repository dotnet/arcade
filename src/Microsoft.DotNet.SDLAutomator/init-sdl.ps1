Param(
  [string] $GuardianCliLocation,
  [string] $Repository,
  [string] $WorkingDirectory,
  [string] $DncEngAccessToken,
  [string] $GuardianLoggerLevel="Standard"
)

$encodedPat = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes(":$DncEngAccessToken"))
$uri = "https://dev.azure.com/dnceng/internal/_apis/git/repositories/sdl-tool-cfg/Items?path=$([Uri]::EscapeDataString("/$Repository/.gdn"))&versionDescriptor[versionOptions]=0&`$format=zip&api-version=5.0-preview.1"
$zipFile = "$WorkingDirectory/gdn.zip"

Add-Type -AssemblyName System.IO.Compression.FileSystem
$gdnFolder = (Join-Path $WorkingDirectory ".gdn")
Try
{
  Write-Host "Downloading gdn folder from internal config repostiory..."
  Invoke-WebRequest -Headers @{ "Accept"="application/zip"; "Authorization"="Basic $encodedPat" } -Uri $uri -OutFile $zipFile
  if (Test-Path $gdnFolder) {
    Remove-Item -Force -Recurse $gdnFolder
  }
  [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $WorkingDirectory)
} Catch [System.Net.WebException] {
  # if the folder does not exist, we'll do a guardian init and push it to the remote repository
  Write-Host "Initializing Guardian..."
  Write-Host "$GuardianCliLocation init --working-directory $WorkingDirectory --logger-level $GuardianLoggerLevel"
  Invoke-Expression "$GuardianCliLocation init --working-directory $WorkingDirectory --logger-level $GuardianLoggerLevel"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian init failed with exit code $LASTEXITCODE."
  }
  Write-Host "$GuardianCliLocation baseline --working-directory $WorkingDirectory --name mainbaseline"
  Invoke-Expression "$GuardianCliLocation baseline --working-directory $WorkingDirectory --name mainbaseline"
  if ($LASTEXITCODE -ne 0) {
    Write-Error "Guardian baseline failed with exit code $LASTEXITCODE."
  }
  & $(Join-Path $PSScriptRoot "push-gdn.ps1") -Repository $Repository -GdnFolder $gdnFolder -DncEngAccessToken $DncEngAccessToken -PushReason "Initialize gdn folder"
}