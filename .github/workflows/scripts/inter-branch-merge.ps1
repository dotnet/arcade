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
    [string[]] $extraShas = & git rev-list "$localRef..$remoteRef" 2>$null
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
    # A merge commit is only created when the merge is conflict-free; if there are
    # ANY conflicts we fall back to a source-only branch so that GitHub surfaces the
    # real conflicts to the reviewer. We deliberately do NOT auto-resolve conflicts
    # client-side, even within ResetToTargetPaths patterns, because client-side
    # resolution can hide conflicts the reviewer needs to see.
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
                # Check whether the remote branch exists before fetching, so we can
                # distinguish "first push to this branch" from "fetch failed".
                & git ls-remote --exit-code --heads $remoteName $mergeBranchName 2>$null | Out-Null
                $remoteBranchExists = ($LASTEXITCODE -eq 0)

                if ($remoteBranchExists) {
                    # Refresh the remote tracking ref so the safety check below uses
                    # current remote state. Fail closed if fetch fails for any reason
                    # other than the branch not existing.
                    & git fetch $remoteName $mergeBranchName 2>&1 | Write-Host
                    if ($LASTEXITCODE -ne 0) {
                        throw "Failed to fetch '$mergeBranchName' from $remoteName (exit code $LASTEXITCODE). Refusing to push without an up-to-date view of the remote branch."
                    }
                }

                if ($createdMergeCommit) {
                    # Merge commits create non-fast-forwardable history on each run,
                    # so we need --force to update the branch. Before force-pushing,
                    # check the remote for commits that aren't reachable from our
                    # local branch AND weren't authored by the bot — those would be
                    # manual changes a contributor pushed to the PR branch.
                    if ($remoteBranchExists) {
                        [string[]] $extraCommits = Get-NonBotExtraCommits $mergeBranchName "origin/$mergeBranchName"
                        if ($extraCommits -and $extraCommits.Count -gt 0) {
                            Write-Warning "Remote branch '$mergeBranchName' has $($extraCommits.Count) non-bot commit(s) not in the local branch. Skipping force push to avoid overwriting manual changes."
                            $extraCommits | % { Write-Warning "  $_" }
                            throw "Remote branch has unmerged human commits"
                        }
                    }
                    Invoke-Block { & git push --force $remoteName "${mergeBranchName}:${mergeBranchName}" }
                } else {
                    # Try non-force push first. If it fails (e.g. remote diverged from
                    # a previous merge-commit run), retry with --force after checking
                    # for human-pushed commits (same guard as the merge-commit path).
                    & git push $remoteName "${mergeBranchName}:${mergeBranchName}" 2>&1 | Write-Host
                    if ($LASTEXITCODE -ne 0) {
                        if ($remoteBranchExists) {
                            [string[]] $extraCommits = Get-NonBotExtraCommits $mergeBranchName "origin/$mergeBranchName"
                            if ($extraCommits -and $extraCommits.Count -gt 0) {
                                Write-Warning "Remote branch '$mergeBranchName' has $($extraCommits.Count) non-bot commit(s) not in the local branch. Skipping force push to avoid overwriting manual changes."
                                $extraCommits | % { Write-Warning "  $_" }
                                throw "Remote branch has unmerged human commits"
                            }
                        }
                        Write-Host "Non-force push failed (likely diverged history). Retrying with --force..."
                        Invoke-Block { & git push --force $remoteName "${mergeBranchName}:${mergeBranchName}" }
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
        # Use --force because the merge branch may have been used for a previous PR.
        # This should only happen if there is no existing PR for the merge

        if ($PSCmdlet.ShouldProcess("Force updating remote branch $mergeBranchName on $remoteName")) {
            Invoke-Block { & git push --force $remoteName "${mergeBranchName}:${mergeBranchName}" }
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
