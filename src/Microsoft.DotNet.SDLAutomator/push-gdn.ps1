Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $GdnFolder,
  [string] $DncEngAccessToken,
  [string] $PushReason
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$sdlDir = "$SourcesDirectory/../sdl-tool-cfg"
if (Test-Path $sdlDir) {
  Remove-Item -Force -Recurse $sdlDir
}

Write-Host "git clone https://dnceng:[DncEngAccessToken]@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir"
git clone https://dnceng:$DncEngAccessToken@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir
if ($LASTEXITCODE -ne 0) {
  Write-Error "Git clone failed with exit code $LASTEXITCODE."
}
Copy-Item -Recurse -Force $GdnFolder $sdlDir/$Repository
Set-Location $sdlDir
Write-Host "git add ."
git add .
if ($LASTEXITCODE -ne 0) {
  Write-Error "Git add failed with exit code $LASTEXITCODE."
}
Write-Host "git commit -m `"$PushReason for $Repository`""
git commit -m "$PushReason for $Repository"
if ($LASTEXITCODE -ne 0) {
  Write-Error "Git commit failed with exit code $LASTEXITCODE."
}
Write-Host "git push"
git push
if ($LASTEXITCODE -ne 0) {
  Write-Error "Git push failed with exit code $LASTEXITCODE."
}