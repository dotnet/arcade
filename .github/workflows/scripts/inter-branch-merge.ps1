#!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request to merge a head branch into a base branch
.PARAMETER RepoOwner
The GitHub repository owner.
.PARAMETER RepoName
The GitHub repository name.
.PARAMETER MergeToBranch
The base branch -- the target branch for the PR
.PARAMETER MergeFromBranch
The current branch
.PARAMETER AllowAutomatedCommits
Create a PR even if the only commits are from dotnet-maestro[bot]
.PARAMETER QuietComments
Do not tag commiters, do not comment on PR updates. Reduces GitHub notifications
.PARAMETER ResetToTargetPaths
Semicolon-separated list of glob patterns for files to reset to the target branch version.
After the merge branch is created, files matching these patterns will be checked out from
the target branch and committed, resolving potential merge conflicts for these files.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Alias('o')]
    [Parameter(Mandatory = $true)]
    $RepoOwner,

    [Alias('n')]
    [Parameter(Mandatory = $true)]
    $RepoName,

    [Alias('b')]
    [Parameter(Mandatory = $true)]
    $MergeToBranch,

    [Alias('h')]
    [Parameter(Mandatory = $true)]
    $MergeFromBranch,

    [switch]$AllowAutomatedCommits,

    [switch]$QuietComments,

    [Alias('r')]
    [string]$ResetToTargetPaths = ""
)

$ErrorActionPreference = 'stop'
Set-StrictMode -Version 1
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$stringToken = $Env:GH_TOKEN

$headers = @{
    Authorization = "bearer $stringToken"
}

[hashtable] $script:emails = @{}

function Invoke-Block([scriptblock]$cmd) {
    Write-Host $cmd.ToString();
    $cmd | Out-String | Write-Verbose
    & $cmd

    # Need to check both of these cases for errors as they represent different items
    # - $?: did the powershell script block throw an error
    # - $lastexitcode: did a windows command executed by the script block end in error
    if ((-not $?) -or ($lastexitcode -ne 0)) {
        if ($error -ne $null) {
            Write-Warning $error[0]
        }
        throw "Command failed to execute: $cmd"
    }
}

function GetCommitterGitHubName($sha) {
    $email = & git show -s --format='%ce' $sha
    $key = 'committer'
    if ($email -eq '@dotnet-maestro') {
        return 'dotnet-maestro'
    }

    # Exclude noreply@github.com - these map to https://github.com/web-flow, which is the user account
    # added as the 'committer' when users commit via the GitHub web UI on their own PRs
    if ((-not $email) -or ($email -eq 'noreply@github.com')) {
        $key = 'author'
        $email = & git show -s --format='%ae' $sha
    }

    if ($email -like '*@users.noreply.github.com') {
        [string[]] $userNames = ($email -replace '@users.noreply.github.com', '') -split '\+'
        return $userNames | select -last 1
    }
    elseif ($script:emails[$email]) {
        return $script:emails[$email]
    }
    else {
        Write-Verbose "Attempting to find GitHub username for $email"
        try {
            $resp = Invoke-RestMethod -Method GET -Headers $headers `
                "https://api.github.com/repos/$RepoOwner/$RepoName/commits/$sha"
            $resp | Write-Verbose

            $script:emails[$email] = $resp.$key.login
            return $resp.$key.login
        }
        catch {
            Write-Warning "Failed to find github user for $email. $_"
        }
    }
    return $null
}

function Get-NonBotExtraCommits($localRef, $remoteRef) {
    # Returns commit SHAs reachable from $remoteRef but not from $localRef
    # that were NOT authored by github-actions[bot]. This lets us safely
    # force-push over our own prior bot merge commits while still protecting
    # human-pushed commits.
    #
    # Fails closed: if `git rev-list` errors (bad ref, transient git failure,
    # partial fetch), we throw rather than treat the result as "no extra
    # commits" — otherwise the caller would silently force-push without the
    # safety check this function is designed to provide.
    [string[]] $extraShas = & git rev-list "$localRef..$remoteRef" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git rev-list '$localRef..$remoteRef' failed (exit code $LASTEXITCODE). Refusing to proceed with force-push without a reliable view of remote commits. Output: $($extraShas -join "`n")"
    }
    if (-not $extraShas -or $extraShas.Count -eq 0) {
        return @()
    }

    $botEmail = '41898282+github-actions[bot]@users.noreply.github.com'
    $nonBot = @()
    foreach ($sha in $extraShas) {
        $authorEmail = & git show -s --format='%ae' $sha 2>$null
        if ($LASTEXITCODE -ne 0 -or -not $authorEmail) {
            # Couldn't read commit metadata — be conservative and treat as non-bot.
            $nonBot += $sha
            continue
        }
        if ($authorEmail.Trim() -ne $botEmail) {
            $nonBot += $sha
        }
    }
    return $nonBot
}

function Update-RemoteBranchInfo($remoteName, $branchName) {
    # Returns @{ Exists = <bool>; Sha = <string-or-null> }. When Exists is true,
    # Sha is the SHA the remote branch points at after a fresh fetch; this is the
    # value the caller should pass to `--force-with-lease=<ref>:<sha>` so that a
    # concurrent push between our fetch and our push is detected atomically by git.
    #
    # Fails closed: if ls-remote says the branch exists but the fetch itself fails
    # (network, auth, etc.) we throw rather than push with a stale view.
    & git ls-remote --exit-code --heads $remoteName $branchName 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        return @{ Exists = $false; Sha = $null }
    }

    & git fetch $remoteName $branchName 2>&1 | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to fetch '$branchName' from $remoteName (exit code $LASTEXITCODE). Refusing to push without an up-to-date view of the remote branch."
    }

    $sha = (& git rev-parse "$remoteName/$branchName" 2>&1).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $sha) {
        throw "Failed to resolve SHA for $remoteName/$branchName after fetch."
    }
    return @{ Exists = $true; Sha = $sha }
}

function Assert-NoHumanCommitsOnRemote($localRef, $remoteRef) {
    # Throws if the remote has commits that aren't reachable from $localRef AND
    # weren't authored by the bot. Use immediately before any force-push to
    # protect manual conflict-resolution pushes from being overwritten.
    [string[]] $extraCommits = Get-NonBotExtraCommits $localRef $remoteRef
    if ($extraCommits -and $extraCommits.Count -gt 0) {
        Write-Warning "Remote branch '$remoteRef' has $($extraCommits.Count) non-bot commit(s) not in the local branch. Skipping force push to avoid overwriting manual changes."
        $extraCommits | % { Write-Warning "  $_" }
        throw "Remote branch has unmerged human commits"
    }
}

function ResetFilesToTargetBranch($patterns, $targetBranch) {
    if (-not $patterns -or $patterns.Count -eq 0) {
        return
    }

    Write-Host "Resetting files to $targetBranch for patterns: $($patterns -join ', ')"

    # Verify the target branch exists
    $branchExists = & git rev-parse --verify "origin/$targetBranch" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Target branch 'origin/$targetBranch' does not exist. Skipping file reset."
        return
    }

    # Note: git user.name/email must already be configured by the caller

    # Track which patterns had changes
    $processedPatterns = @()

    foreach ($pattern in $patterns) {
        $pattern = $pattern.Trim()
        if (-not $pattern) {
            continue
        }

        Write-Host "Processing pattern: $pattern"
        
        # Use git checkout to reset files matching the pattern to the target branch
        # The -- is needed to separate the revision from the pathspec
        # Just attempt to checkout the pattern directly - git will handle whether files exist
        try {
            & git checkout "origin/$targetBranch" -- $pattern 2>&1 | Write-Host
            if ($LASTEXITCODE -eq 0) {
                Write-Host -f Green "Checked out pattern '$pattern' from $targetBranch"
                $processedPatterns += $pattern
            } else {
                Write-Host -f Yellow "Pattern '$pattern' did not match any files in $targetBranch"
            }
        }
        catch {
            Write-Warning "Failed to checkout pattern '$pattern' from $targetBranch. Error: $_"
        }
    }

    # Check if there are any changes to commit after processing all patterns
    $status = & git status --porcelain
    if ($status -and $processedPatterns.Count -gt 0) {
        # Add all changes (the checkout already modified the specific files)
        Invoke-Block { & git add -A }
        
        # Create a commit message listing all patterns that were reset
        $patternsList = $processedPatterns -join "`n- "
        $commitMessage = "Reset files to $targetBranch`n`nReset patterns:`n- $patternsList"
        
        Invoke-Block { & git commit -m $commitMessage }
        Write-Host -f Green "Successfully reset files to $targetBranch for patterns: $patternsList"
    } else {
        Write-Host "No changes to commit after processing all patterns"
    }
}

# see https://git-scm.com/docs/pretty-formats
$formatString = '%h %cn <%ce>: %s (%cr)'

try {
    Invoke-Block { & git fetch --quiet origin }
    Invoke-Block { & git checkout --quiet $MergeToBranch }
    Invoke-Block { & git reset --hard origin/$MergeToBranch }

    Write-Host -f Magenta "${MergeToBranch}:`t`t$(& git log --format=$formatString -1 HEAD)"

    Invoke-Block { & git checkout --quiet $MergeFromBranch }
    Invoke-Block { & git reset --quiet --hard origin/$MergeFromBranch }

    Write-Host -f Magenta "${MergeFromBranch}:`t$(& git log --format=$formatString -1 HEAD)"

    [string[]] $commitsToMerge = & git rev-list "$MergeToBranch..$MergeFromBranch" # find all commits which will be merged

    if (-not $commitsToMerge) {
        Write-Warning "There were no commits to be merged from $MergeFromBranch into $MergeToBranch"
        exit 0
    }

    $authors = $commitsToMerge `
        | % { Write-Host -f Cyan "Merging:`t$(git log --format=$formatString -1 $_)"; $_ } `
        | % { GetCommitterGitHubName $_ } `
        | ? { $_ -ne $null } `
        | select -Unique

    if (-not $AllowAutomatedCommits -and (($authors | measure).Count -eq 1) -and ($authors | select -first 1) -eq 'dotnet-maestro[bot]') {
        Write-Host -ForegroundColor Yellow 'Skipping PR generation because it appears this PR would only contain automated commits by @dotnet-maestro[bot]'
        exit 0
    }

    if (-not $QuietComments) {
        $authors = $authors | % { "* @$_" }
    } else {
        $authors = $authors | % { "* $_" }
    }
    
    $committersList = "This PR merges commits made on $MergeFromBranch by the following committers:`n`n$($authors -join "`n")"

    Write-Host $committersList

    $mergeBranchName = "merge/$MergeFromBranch-to-$MergeToBranch"

    # Track whether we created a merge commit (affects push strategy and PR comment).
    # A merge commit is only created when the merge is conflict-free; if ANY file
    # outside the ResetToTargetPaths patterns conflicts, we fall back to a
    # source-only branch so GitHub's merge button surfaces those conflicts to the
    # reviewer. Files INSIDE the ResetToTargetPaths patterns are always reset to
    # the target branch version after the merge step — that is the documented
    # opt-in behavior of the parameter (see the .PARAMETER block above). We do
    # NOT use `-X ours` / `-X theirs` or any other merge-strategy option to
    # auto-resolve conflicts in files outside those patterns.
    $createdMergeCommit = $false
    [string[]] $conflictFiles = @()

    # When ResetToTargetPaths is configured, we attempt to create a proper merge commit
    # so that the target branch content is included. If the merge has any conflicts we
    # fall back to the original source-only behavior so GitHub's merge button surfaces
    # them to the reviewer.
    if ($ResetToTargetPaths) {
        # Configure git user for the merge commit
        Invoke-Block { & git config user.name "github-actions[bot]" }
        Invoke-Block { & git config user.email "41898282+github-actions[bot]@users.noreply.github.com" }

        $patterns = ($ResetToTargetPaths -split ";") | % { $_.Trim() } | ? { $_ }

        # Start from the target branch and merge source into it
        Invoke-Block { & git checkout -B $mergeBranchName "origin/$MergeToBranch" }

        # Try a clean merge. We do NOT pass -X ours / -X theirs anywhere in this
        # script — any conflict must surface to the reviewer via the source-only
        # fallback below.
        $mergeOutput = & git merge --no-ff "origin/$MergeFromBranch" -m "Merge branch '$MergeFromBranch' into $MergeToBranch" 2>&1
        $mergeExitCode = $LASTEXITCODE

        # Always log merge output for CI diagnostics
        if ($mergeOutput) {
            $mergeOutput | Write-Host
        }

        if ($mergeExitCode -eq 0) {
            $createdMergeCommit = $true
        } else {
            # Capture conflict file list before aborting so we can surface it.
            [string[]] $conflictFiles = & git -c core.quotePath=false diff --name-only --diff-filter=U

            # Abort the conflicted merge before proceeding.
            # Use plain call (not Invoke-Block) because git merge --abort exits 128
            # if there is no merge-in-progress (e.g. a non-conflict git failure).
            & git merge --abort 2>&1 | Write-Host

            if (-not $conflictFiles -or $conflictFiles.Count -eq 0) {
                Write-Host -f Yellow "Merge failed with exit code $mergeExitCode but no conflicts were detected."
                Write-Host -f Yellow "Falling back to source-only branch."
            } else {
                Write-Host -f Yellow "Merge produced conflicts in the following files:"
                $conflictFiles | % { Write-Host -f Yellow "  - $_" }
                Write-Host -f Yellow "Falling back to source-only branch so GitHub surfaces these conflicts in the PR."
            }

            Invoke-Block { & git checkout -B $mergeBranchName "origin/$MergeFromBranch" }
        }

        ResetFilesToTargetBranch $patterns $MergeToBranch
    }
    else {
        # Without ResetToTargetPaths, the original behavior is fine: create a branch
        # from the source and let GitHub's merge button do the actual merge.
        Invoke-Block { & git checkout -B $mergeBranchName "origin/$MergeFromBranch" }
    }

    $remoteName = 'origin'
    $prOwnerName = $RepoOwner
    $prRepoName = $RepoName

    $query = 'query ($repoOwner: String!, $repoName: String!, $baseRefName: String!, $headRefName: String!) {
        repository(owner: $repoOwner, name: $repoName) {
          pullRequests(baseRefName: $baseRefName, headRefName: $headRefName, states: OPEN, first: 100) {
            totalCount
            nodes {
              number
              headRef {
                name
                repository {
                  name
                  owner {
                    login
                  }
                }
              }
            }
          }
        }
      }'

    $data = @{
        query     = $query
        variables = @{
            repoOwner   = $RepoOwner
            repoName    = $RepoName
            baseRefName = $MergeToBranch
            headRefName = $mergeBranchName
        }
    }

    $resp = Invoke-RestMethod -Method Post `
        -Headers $headers `
        https://api.github.com/graphql `
        -Body ($data | ConvertTo-Json)
    $resp | Write-Verbose

    $matchingPr = $resp.data.repository.pullRequests.nodes `
        | ? { $_.headRef.name -eq $mergeBranchName -and $_.headRef.repository.owner.login -eq $prOwnerName } `
        | select -First 1

    if ($matchingPr) {
        $prUpdatedSuccess = $false

        try {
            if ($PSCmdlet.ShouldProcess("Update remote branch $mergeBranchName on $remoteName")) {
                # Refresh remote tracking info and capture the current remote SHA so
                # we can pass it to --force-with-lease later. Update-RemoteBranchInfo
                # fails closed on any fetch error other than "branch doesn't exist".
                $remoteInfo = Update-RemoteBranchInfo $remoteName $mergeBranchName
                $remoteBranchExists = $remoteInfo.Exists
                $remoteSha = $remoteInfo.Sha

                if ($createdMergeCommit) {
                    # Merge commits create non-fast-forwardable history on each run,
                    # so we need --force to update the branch. Before force-pushing,
                    # check the remote for commits that aren't reachable from our
                    # local branch AND weren't authored by the bot — those would be
                    # manual changes a contributor pushed to the PR branch.
                    if ($remoteBranchExists) {
                        Assert-NoHumanCommitsOnRemote $mergeBranchName "origin/$mergeBranchName"
                        # --force-with-lease closes the TOCTOU window between our
                        # fetch and our push: if anyone (human or bot) pushes to the
                        # remote between Update-RemoteBranchInfo and this push, git
                        # rejects our push instead of silently overwriting them.
                        Invoke-Block { & git push --force-with-lease="refs/heads/${mergeBranchName}:${remoteSha}" $remoteName "${mergeBranchName}:${mergeBranchName}" }
                    } else {
                        # First push to this branch — nothing to lease against.
                        Invoke-Block { & git push $remoteName "${mergeBranchName}:${mergeBranchName}" }
                    }
                } else {
                    # Source-only branch path. Try a non-force push first — if the
                    # branch is brand new or strictly ahead of the remote, this
                    # succeeds. Only retry with --force-with-lease if the failure
                    # looks like a non-fast-forward; any other failure (auth,
                    # network, permission) must NOT silently escalate to force.
                    $pushOutput = & git push $remoteName "${mergeBranchName}:${mergeBranchName}" 2>&1
                    $pushExit = $LASTEXITCODE
                    if ($pushOutput) {
                        $pushOutput | Write-Host
                    }
                    if ($pushExit -ne 0) {
                        $pushOutputText = ($pushOutput | Out-String)
                        $isNonFastForward = $pushOutputText -match '(?i)non-fast-forward|failed to push some refs|tip of your.*is behind|Updates were rejected'
                        if (-not $isNonFastForward) {
                            throw "git push failed with exit code $pushExit and the failure does not look like a non-fast-forward (auth/network/permission?). Refusing to auto-retry with --force. Push output: $pushOutputText"
                        }

                        if (-not $remoteBranchExists) {
                            # We previously decided the branch did not exist, but the
                            # push still rejected as non-fast-forward. Re-check the
                            # remote — most likely a concurrent run created the
                            # branch between Update-RemoteBranchInfo and this push.
                            $remoteInfo = Update-RemoteBranchInfo $remoteName $mergeBranchName
                            $remoteBranchExists = $remoteInfo.Exists
                            $remoteSha = $remoteInfo.Sha
                        }

                        if ($remoteBranchExists) {
                            Assert-NoHumanCommitsOnRemote $mergeBranchName "origin/$mergeBranchName"
                            Write-Host "Non-force push failed with non-fast-forward. Retrying with --force-with-lease..."
                            Invoke-Block { & git push --force-with-lease="refs/heads/${mergeBranchName}:${remoteSha}" $remoteName "${mergeBranchName}:${mergeBranchName}" }
                        } else {
                            throw "Non-fast-forward rejection but remote branch still reports as not-existing. Refusing to force-push blindly."
                        }
                    }
                }
            }
            $prUpdatedSuccess = $true
        }
        catch {
            Write-Warning "Failed to update existing PR: $_"
        }

        # Build the PR update comment. Tell reviewers which merge path was taken so
        # they know whether GitHub's diff reflects a real merge or a source-only
        # branch (i.e. whether they need to use the merge button to resolve
        # conflicts).
        if ($prUpdatedSuccess) {
            if ($createdMergeCommit) {
                $pathDescription = "This pull request was updated with a clean merge commit (no conflicts)."
            } elseif ($conflictFiles -and $conflictFiles.Count -gt 0) {
                $conflictList = ($conflictFiles | % { "  - ``$_``" }) -join "`n"
                $pathDescription = @"
This pull request was updated **without** a merge commit because the following file(s) had conflicts that must be resolved manually via GitHub's merge button:

$conflictList
"@
            } else {
                $pathDescription = "This pull request was updated."
            }
            $prMessage = "$pathDescription`n`n$committersList"
        } else {
            $prMessage = @"
:x: Uh oh, this pull request could not be updated automatically. New commits were pushed to $MergeFromBranch, but I could not automatically push those to $mergeBranchName to update this PR.
You may need to fix this problem by merging branches with this PR. Contact .NET Core Engineering if you are not sure what to do about this.
"@
        }

        $data = @{
            body = $prMessage
        }

        $prNumber = $matchingPr.number
        $prUrl = "https://github.com/$RepoOwner/$RepoName/pull/$prNumber"

        if ($PSCmdlet.ShouldProcess("Update $prUrl") -and -not $QuietComments) {
            $resp = Invoke-RestMethod -Method Post -Headers $headers `
                "https://api.github.com/repos/$RepoOwner/$RepoName/issues/$prNumber/comments" `
                -Body ($data | ConvertTo-Json)
            $resp | Write-Verbose
            Write-Host -f green "Updated pull request $url"
        }
    }
    else {
        # No open PR matched — we'll create one below. The merge branch may still
        # exist remotely from a closed PR or a manual contributor push. Apply the
        # same human-commit guard + --force-with-lease as the PR-update path so
        # we never silently overwrite human commits, and re-fetch atomically so
        # the lease SHA matches the latest remote state.

        if ($PSCmdlet.ShouldProcess("Force updating remote branch $mergeBranchName on $remoteName")) {
            $remoteInfo = Update-RemoteBranchInfo $remoteName $mergeBranchName
            if ($remoteInfo.Exists) {
                Assert-NoHumanCommitsOnRemote $mergeBranchName "origin/$mergeBranchName"
                Invoke-Block { & git push --force-with-lease="refs/heads/${mergeBranchName}:$($remoteInfo.Sha)" $remoteName "${mergeBranchName}:${mergeBranchName}" }
            } else {
                # First push to this branch — nothing to lease against.
                Invoke-Block { & git push $remoteName "${mergeBranchName}:${mergeBranchName}" }
            }
        }

        $previewHeaders = @{
            #  Required while this api is in preview: https://developer.github.com/v3/pulls/#create-a-pull-request
            Accept        = 'application/vnd.github.symmetra-preview+json'
            Authorization = "bearer $stringToken"
        }

        $prBody = @"
I detected changes in the $MergeFromBranch branch which have not been merged yet to $MergeToBranch. I'm a robot and am configured to help you automatically keep $MergeToBranch up to date, so I've opened this PR.

$committersList

## Instructions for merging from UI

This PR will not be auto-merged. When pull request checks pass, complete this PR by creating a merge commit, *not* a squash or rebase commit.

<img alt="merge button instructions" src="https://i.imgur.com/GepcNJV.png" width="300" />

If this repo does not allow creating merge commits from the GitHub UI, use command line instructions.

## Instructions for merging via command line

Run these commands to merge this pull request from the command line.

`````` sh
git fetch
git checkout ${MergeFromBranch}
git pull --ff-only
git checkout ${MergeToBranch}
git pull --ff-only
git merge --no-ff ${MergeFromBranch}

# If there are merge conflicts, resolve them and then run `git merge --continue` to complete the merge
# Pushing the changes to the PR branch will re-trigger PR validation.
git push https://github.com/$prOwnerName/$prRepoName HEAD:${mergeBranchName}
``````

<details>
<summary>or if you are using SSH</summary>

``````
git push git@github.com:$prOwnerName/$prRepoName HEAD:${mergeBranchName}
``````

</details>


After PR checks are complete push the branch
``````
git push
``````

## Instructions for resolving conflicts

:warning: If there are merge conflicts, you will need to resolve them manually before merging. You can do this [using GitHub][resolve-github] or using the [command line][resolve-cli].

[resolve-github]: https://help.github.com/articles/resolving-a-merge-conflict-on-github/
[resolve-cli]: https://help.github.com/articles/resolving-a-merge-conflict-using-the-command-line/

## Instructions for updating this pull request

Contributors to this repo have permission update this pull request by pushing to the branch '$mergeBranchName'. This can be done to resolve conflicts or make other changes to this pull request before it is merged.
The provided examples assume that the remote is named 'origin'. If you have a different remote name, please replace 'origin' with the name of your remote.

``````
git fetch
git checkout -b ${mergeBranchName} origin/$MergeToBranch
git pull https://github.com/$prOwnerName/$prRepoName ${mergeBranchName}
(make changes)
git commit -m "Updated PR with my changes"
git push https://github.com/$prOwnerName/$prRepoName HEAD:${mergeBranchName}
``````

<details>
    <summary>or if you are using SSH</summary>

``````
git fetch
git checkout -b ${mergeBranchName} origin/$MergeToBranch
git pull git@github.com:$prOwnerName/$prRepoName ${mergeBranchName}
(make changes)
git commit -m "Updated PR with my changes"
git push git@github.com:$prOwnerName/$prRepoName HEAD:${mergeBranchName}
``````

</details>

Contact .NET Core Engineering (dotnet/dnceng) if you have questions or issues.
Also, if this PR was generated incorrectly, help us fix it. See https://github.com/dotnet/arcade/blob/main/.github/workflows/scripts/inter-branch-merge.ps1.

"@;

        $data = @{
            title                 = "[automated] Merge branch '$MergeFromBranch' => '$MergeToBranch'"
            head                  = "${prOwnerName}:${mergeBranchName}"
            base                  = $MergeToBranch
            body                  = $prBody
            maintainer_can_modify = $true
        }

        if ($PSCmdlet.ShouldProcess("Create PR from ${prOwnerName}:${mergeBranchName} to $MergeToBranch on $Reponame")) {
            $resp = Invoke-RestMethod -Method POST -Headers $previewHeaders `
                https://api.github.com/repos/$RepoOwner/$RepoName/pulls `
                -Body ($data | ConvertTo-Json)
            $resp | Write-Verbose
            Write-Host -f green "Created pull request https://github.com/$RepoOwner/$RepoName/pull/$($resp.number)"
        }
    }
}
finally {
    Pop-Location
}
