# Validates that Arcade build promotion works when driven by the *newly built* Arcade (rather than
# the bootstrap Arcade pinned in global.json). This is one of the parallel validations that make up
# the official build's ValidateSdk stage; it does NOT perform the final promotion to the release
# channel (that is done separately, with the default Arcade, only after all validation succeeds).
#
# Mirrors dotnet/arcade-validation's eng/validation/test-publishing.ps1:
#   1. Create a dev branch off the commit that produced this build and run
#      `darc update-dependencies --id <BuildId>` to bump Arcade to the version this build produced;
#      push it to the internal AzDO mirror.
#   2. Promote the build to a test channel ("General Testing") with --source-branch <devBranch>. Darc
#      runs the promotion (publishing) pipeline from that branch, so PublishArtifactsInManifest /
#      Microsoft.DotNet.Build.Tasks.Feed come from the NEWLY BUILT Arcade -> promotion is validated
#      against the new Arcade.
#   3. Delete the dev branch.
#
# NOTE: runs only in the internal official build; not testable locally or in PR. Needs
# @dotnet/dnceng review and a real official-build run to confirm.

Param(
  [Parameter(Mandatory=$true)][int]    $BuildId,     # BAR build id of the build being validated.
  [Parameter(Mandatory=$true)][string] $Commit,      # The commit that produced this build (Build.SourceVersion).
  [Parameter(Mandatory=$true)][string] $AzdoToken,   # AzDO OAuth/AAD access token (WIF), not a PAT; needs code read/write on the mirror.
  [Parameter(Mandatory=$true)][string] $GitHubPat,   # GitHub PAT; darc update-dependencies --id needs it for coherency updates.
  [string] $AzdoOrg      = 'dnceng',
  [string] $AzdoProject  = 'internal',
  [string] $AzdoRepoName = 'dotnet-arcade',
  [string] $TestChannel  = 'General Testing'
)

set-strictmode -version 2.0
$ErrorActionPreference = 'Stop'

. $PSScriptRoot\..\common\tools.ps1

# Install a private copy of darc so we don't disturb the repo's tool manifest.
$darcPath = Join-Path $PSScriptRoot "darc\$([guid]::NewGuid())"
& $PSScriptRoot\..\common\darc-init.ps1 -toolpath $darcPath | Out-Host
$darc = Join-Path $darcPath 'darc.exe'

$targetBranch = "validation/promote-arcade-$BuildId"
$repoUri = "https://dev.azure.com/$AzdoOrg/$AzdoProject/_git/$AzdoRepoName"

# Push using the AzDO token via an http extraheader so we don't persist credentials on disk.
$authHeader = "Authorization: Bearer $AzdoToken"

# Work in an agent-local scratch directory.
$scratchBase = if ($env:AGENT_TEMPDIRECTORY) { $env:AGENT_TEMPDIRECTORY } else { [System.IO.Path]::GetTempPath() }
$repoRoot = Join-Path $scratchBase "promote-$BuildId"
if (Test-Path $repoRoot) { Remove-Item -Recurse -Force $repoRoot }

try {
  Write-Host "Cloning '$AzdoRepoName' and creating branch '$targetBranch' at $Commit."
  & git -c "http.extraheader=$authHeader" clone -c core.longpaths=true $repoUri $repoRoot
  if ($LASTEXITCODE -ne 0) { throw "git clone failed." }

  Push-Location $repoRoot
  try {
    & git config user.email 'dotnet-maestro[bot]@users.noreply.github.com'
    & git config user.name 'dotnet-maestro[bot]'
    & git checkout -b $targetBranch $Commit
    if ($LASTEXITCODE -ne 0) { throw "git checkout of $Commit failed." }

    # Bump Arcade (and coherent dependencies) to the versions this build produced. The --id form
    # performs coherency updates, which require a GitHub PAT.
    & $darc update-dependencies --id $BuildId --azdev-pat $AzdoToken --github-pat $GitHubPat --ci
    if ($LASTEXITCODE -ne 0) { throw "darc update-dependencies failed." }

    # A no-op update means darc produced the same versions already pinned, which is unexpected for a
    # new build - treat it as an error rather than silently promoting an unchanged branch.
    & git add -A
    if ([string]::IsNullOrWhiteSpace((& git status --porcelain))) {
      throw "darc update-dependencies produced no changes; the build produced the same versions already pinned."
    }
    & git commit -m "Update Arcade to the newly built version for promotion validation (BAR $BuildId)"
    if ($LASTEXITCODE -ne 0) { throw "git commit failed." }

    & git -c "http.extraheader=$authHeader" push origin "HEAD:refs/heads/$targetBranch"
    if ($LASTEXITCODE -ne 0) { throw "git push of '$targetBranch' failed." }
  }
  finally {
    Pop-Location
  }

  try {
    # Validate promotion with the new Arcade: darc runs the promotion pipeline from $targetBranch,
    # whose global.json now points at the newly built Arcade, so the new publishing infra does the
    # work. Let darc pick the publishing infra version (do not pin it here).
    Write-Host "Validating promotion with the newly built Arcade by promoting build $BuildId to '$TestChannel' from '$targetBranch'."
    & $darc add-build-to-channel `
        --id $BuildId `
        --channel "$TestChannel" `
        --source-branch $targetBranch `
        --azdev-pat $AzdoToken `
        --ci
    if ($LASTEXITCODE -ne 0) { throw "darc add-build-to-channel to '$TestChannel' failed." }
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
  Write-PipelineTelemetryError -Category 'ValidatePromotion' -Message $_
  ExitWithExitCode 1
}

ExitWithExitCode 0
