Param(
  [string] $Repository,
  [string] $GdnFolder,
  [string] $DncEngAccessToken,
  [string] $PushReason
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$sdlDir = Join-Path $env:TEMP "sdl"
if (Test-Path $sdlDir) {
  Remove-Item -Force -Recurse $sdlDir
}

Write-Host "git clone https://dnceng:`$DncEngAccessToken@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir"
git clone https://dnceng:$DncEngAccessToken@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir
if ($LASTEXITCODE -ne 0) {
  Write-Error "Git clone failed with exit code $LASTEXITCODE."
}
$sdlRepositoryFolder = Join-Path (Join-Path $sdlDir $Repository) ".gdn"
if (Get-Command Robocopy) {
  Robocopy /S $GdnFolder $sdlRepositoryFolder
} else {
  rsync -r $GdnFolder $sdlRepositoryFolder
}
$currentDirectory = (Get-Location).Path
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

Set-Location $currentDirectory