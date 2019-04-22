Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $DncengPat,
  [string] $GdnLoggerLevel="Standard"
)

$encodedPat = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes(":$DncengPat"))
$uri = "https://dev.azure.com/dnceng/internal/_apis/git/repositories/sdl-tool-cfg/Items?path=$([Uri]::EscapeDataString("/$Repository/.gdn"))&versionDescriptor[versionOptions]=0&`$format=zip&api-version=5.0-preview.1"
$zipFile = "$SourcesDirectory/gdn.zip"
Try
{
  Invoke-WebRequest -Headers @{ "Accept"="application/zip"; "Authorization"="Basic $encodedPat" } -Uri $uri -OutFile $zipFile
  [System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $sourcesDirectory)
} Catch [System.Net.WebException] {
  # if the folder does not exist, we'll do a gdn init and create a PR for it
  Write-Host "Initializing Guardian..."
  guardian init --logger-level $GdnLoggerLevel
  Invoke-Expression "push-gdn.ps1 -Repository $Repository -SourcesDirectory $SourcesDirectory -GdnFolder $SourcesDirectory/.gdn -DncengPat $DncengPat -PushReason `"Initialize gdn folder`""
}