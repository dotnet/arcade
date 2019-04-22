Param(
  [string] $Repository,
  [string] $SourcesDirectory,
  [string] $GdnFolder,
  [string] $DncengPat,
  [string] $PushReason
)

$sdlDir = "$SourcesDirectory/../sdl-tool-cfg"
if (Test-Path $sdlDir) {
  Remove-Item -Force -Recurse $sdlDir
}

Write-Host "git clone https://dnceng:$DncengPat@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir"
git clone https://dnceng:$DncengPat@dev.azure.com/dnceng/internal/_git/sdl-tool-cfg $sdlDir
Copy-Item -Recurse -Force $GdnFolder $SourcesDirectory/../sdl-tool-cfg/$Repository
Set-Location $sdlDir
Write-Host "git add ."
git add .
Write-Host "git commit -m `"$PushReason for $Repository`""
git commit -m "$PushReason for $Repository"
Write-Host "git push"
git push