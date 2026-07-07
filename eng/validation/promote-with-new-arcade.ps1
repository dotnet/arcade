# Promotes an Arcade build to the final channel, validating that promotion works when driven by the
# *newly built* Arcade rather than the bootstrap Arcade pinned in global.json.
#
# This replaces the promotion validation that dotnet/arcade-validation performed (see that repo's
# eng/validation/test-publishing.ps1 and update-channel.ps1). The flow mirrors it:
#
#   1. The official build has already published this build's assets to the source channel
#      (".NET Eng - Validation") using the current/bootstrap Arcade (normal post-build publishing).
#   2. Create a dev branch off the built commit, run `darc update-dependencies --id <BARBuildId>` to
#      bump Arcade to the version this build produced, and push it to the internal AzDO mirror.
#   3. Promote the build to a test channel ("General Testing") with --source-branch <devBranch>. Darc
#      runs the promotion (publishing) pipeline from that branch, so PublishArtifactsInManifest /
#      Microsoft.DotNet.Build.Tasks.Feed come from the NEWLY BUILT Arcade -> this validates promotion
#      with the new Arcade.
#   4. On success, add the build to the final channel (".NET Eng - Latest") with
#      --skip-assets-publishing (assets are already published; this is the promotion gate).
#   5. Delete the dev branch.
#
# When -UsePrebuiltArcade:$false, steps 2-4 are collapsed into a plain promotion to the final channel
# using the current Arcade (the bootstrap-breakage escape hatch).
#
# NOTE: This only runs in the internal official build and cannot be validated locally or in PR. It
# requires review by @dotnet/dnceng and a real official-build run to confirm.

Param(
  [Parameter(Mandatory=$true)][int]    $BuildId,           # BAR build id of the build being promoted.
  [Parameter(Mandatory=$true)][string] $AzdoToken,         # AzDO token with code read/write on the mirror (System.AccessToken).
  [string] $AzdoOrg          = 'dnceng',
  [string] $AzdoProject      = 'internal',
  [string] $AzdoRepoName     = 'dotnet-arcade',
  [string] $SourceChannel    = '.NET Eng - Validation',
  [string] $TestChannel      = 'General Testing',
  [string] $FinalChannel     = '.NET Eng - Latest',
  # 'true' (default): validate promotion with the newly built Arcade before the final promotion.
  # Anything else: escape hatch - promote to the final channel with the current (global.json) Arcade.
  [string] $UsePrebuiltArcade = 'true'
)

set-strictmode -version 2.0
$ErrorActionPreference = 'Stop'

. $PSScriptRoot\..\common\tools.ps1

# Install a private copy of darc so we don't disturb the repo's tool manifest.
$darcPath = Join-Path $PSScriptRoot "darc\$(New-Guid)"
& $PSScriptRoot\..\common\darc-init.ps1 -toolpath $darcPath | Out-Host
$darc = Join-Path $darcPath 'darc.exe'

function Invoke-Darc {
  Write-Host "darc $args"
  & $darc @args --azdev-pat $AzdoToken --ci
  if ($LASTEXITCODE -ne 0) {
    throw "darc exited with code $LASTEXITCODE"
  }
}

function Add-BuildToChannel([string] $channel, [string[]] $extraArgs) {
  $darcArgs = @('add-build-to-channel', '--id', $BuildId, '--channel', $channel) + $extraArgs
  Invoke-Darc @darcArgs
}

try {
  if ($UsePrebuiltArcade -ne 'true') {
    # Escape hatch: promote to the final channel with the current (global.json) Arcade.
    Write-Host "UsePrebuiltArcade is '$UsePrebuiltArcade': promoting build $BuildId to '$FinalChannel' with the current Arcade."
    Add-BuildToChannel $FinalChannel @('--skip-assets-publishing')
    Write-Host 'done.'
    ExitWithExitCode 0
  }

  # Discover the commit this build was produced from so we can branch off it.
  $buildJson = & $darc get-build --id $BuildId --output-format json --ci | ConvertFrom-Json
  if ($LASTEXITCODE -ne 0 -or $null -eq $buildJson) {
    throw "Could not retrieve BAR build $BuildId."
  }
  $sha = $buildJson.commit

  $targetBranch = "validation/promote-arcade-$BuildId"
  $repoUri = "https://dev.azure.com/$AzdoOrg/$AzdoProject/_git/$AzdoRepoName"

  # Work in an agent-local scratch directory.
  $scratchBase = if ($env:AGENT_TEMPDIRECTORY) { $env:AGENT_TEMPDIRECTORY } else { [System.IO.Path]::GetTempPath() }
  $repoRoot = Join-Path $scratchBase "promote-$BuildId"
  if (Test-Path $repoRoot) { Remove-Item -Recurse -Force $repoRoot }

  # Push using the AzDO token via an http extraheader so we don't persist credentials on disk.
  $authHeader = "AUTHORIZATION: bearer $AzdoToken"

  Write-Host "Cloning '$AzdoRepoName' and creating branch '$targetBranch' at $sha."
  & git -c "http.extraheader=$authHeader" clone -c core.longpaths=true $repoUri $repoRoot
  if ($LASTEXITCODE -ne 0) { throw "git clone failed." }

  Push-Location $repoRoot
  try {
    & git config user.email 'dotnet-maestro[bot]@users.noreply.github.com'
    & git config user.name 'dotnet-maestro[bot]'
    & git checkout -b $targetBranch $sha
    if ($LASTEXITCODE -ne 0) { throw "git checkout of $sha failed." }

    # Bump Arcade (and coherent dependencies) to the versions this build produced.
    & $darc update-dependencies --id $BuildId --azdev-pat $AzdoToken --ci
    if ($LASTEXITCODE -ne 0) { throw "darc update-dependencies failed." }

    & git commit -am "Update Arcade to the newly built version for promotion validation (BAR $BuildId)"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

    & git -c "http.extraheader=$authHeader" push origin HEAD
    if ($LASTEXITCODE -ne 0) { throw "git push of '$targetBranch' failed." }
  }
  finally {
    Pop-Location
  }

  try {
    # Test promotion with the new Arcade: darc runs the promotion pipeline from $targetBranch, whose
    # global.json now points at the newly built Arcade, so the new publishing infra does the work.
    Write-Host "Validating promotion with the newly built Arcade by promoting build $BuildId to '$TestChannel' from '$targetBranch'."
    Add-BuildToChannel $TestChannel @('--source-branch', $targetBranch, '--publishing-infra-version', '3')

    # The new Arcade published assets successfully; register the build on the final channel.
    Write-Host "Promotion validated. Adding build $BuildId to '$FinalChannel'."
    Add-BuildToChannel $FinalChannel @('--skip-assets-publishing')
  }
  finally {
    Write-Host "Cleaning up branch '$targetBranch'."
    try {
      & git -c "http.extraheader=$authHeader" -C $repoRoot push origin --delete $targetBranch
    }
    catch {
      Write-Warning "Unable to delete branch '$targetBranch': $_"
    }
  }

  Write-Host 'done.'
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'PromoteWithNewArcade' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
