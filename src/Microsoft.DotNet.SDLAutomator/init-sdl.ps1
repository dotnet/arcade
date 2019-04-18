Param(
  [string] $repository,
  [string] $sourcesDirectory,
  [string] $dncengPat,
)

$encodedPat = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes(":$dncengPat"))
$uri = "https://dev.azure.com/dnceng/internal/_apis/git/repositories/sdl-tool-cfg/Items?path=$([Uri]::EscapeDataString("/$repository/.gdn"))&versionDescriptor[versionOptions]=0&`$format=zip&api-version=5.0-preview.1"
$zipFile = "$sourcesDirectory/gdn.zip"
Invoke-WebRequest -Headers @{ "Accept"="application/zip"; "Authorization"="Basic $encodedPat" } -Uri $uri -OutFile $zipFile
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $sourcesDirectory)
