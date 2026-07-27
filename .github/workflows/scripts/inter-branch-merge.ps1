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

function RemoteBranchExists($remoteName, $branchName) {
    $lsRemoteOutput = & git ls-remote --heads $remoteName "refs/heads/$branchName" 2>&1
    if ($LASTEXITCODE -ne 0) {
        # Fail loudly instead of assuming the branch is missing: treating an auth or network
        # failure as "branch does not exist" would silently recreate the branch and discard
        # whatever is already on the PR.
        throw "Failed to query '$remoteName' for branch '$branchName'. Output: $lsRemoteOutput"
    }

    return [bool]$lsRemoteOutput
}

# Resolves merge conflicts that fall entirely within the ResetToTargetPaths patterns.
#
# Those files are expected to conflict: the merge branch holds the target branch's version of them
# while the source branch keeps changing them. They are not conflicts anyone reviews -- the script
# overwrites those exact paths with the target branch's content on every run no matter how the merge
# turns out -- so taking the target's version here produces the same tree the script would have
# produced anyway. Nothing outside the configured patterns is ever resolved: a single conflict
# outside them makes this return $false so the caller aborts the merge and leaves it to a human.
function TryResolveResetPathConflicts($patterns, $targetBranch) {
    if (-not $patterns -or $patterns.Count -eq 0) {
        return $false
    }

    $conflicted = @(& git diff --name-only --diff-filter=U)
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list conflicted files after merging into the existing merge branch."
    }

    if ($conflicted.Count -eq 0) {
        return $false
    }

    # Pathspec magic (anything starting with ':', e.g. ':(attr:...)' or ':(exclude)') can select a
    # different set of files depending on repository state, so which files a pattern covers is not
    # guaranteed to still hold after resolving. Only ordinary paths and globs are safe to reason
    # about here; anything else falls back to leaving the merge to a human.
    $magicPatterns = @($patterns | Where-Object { $_ -and $_.Trim().StartsWith(':') })
    if ($magicPatterns.Count -gt 0) {
        Write-Host -f Yellow "ResetToTargetPaths uses pathspec magic ($($magicPatterns -join ', ')); not resolving conflicts automatically."
        return $false
    }

    $covered = @()
    foreach ($pattern in $patterns) {
        $pattern = $pattern.Trim()
        if (-not $pattern) {
            continue
        }

        $matched = @(& git diff --name-only --diff-filter=U -- $pattern)
        if ($LASTEXITCODE -eq 0 -and $matched.Count -gt 0) {
            $covered += $matched
        }
    }

    $covered = @($covered | Select-Object -Unique)

    $uncovered = @($conflicted | Where-Object { $covered -notcontains $_ })
    if ($uncovered.Count -gt 0) {
        Write-Host -f Yellow "Conflicts outside ResetToTargetPaths, leaving them for manual resolution: $($uncovered -join ', ')"
        return $false
    }

    foreach ($file in $covered) {
        & git checkout "origin/$targetBranch" -- $file 2>&1 | Write-Host
        if ($LASTEXITCODE -ne 0) {
            Write-Host -f Yellow "Could not take the '$targetBranch' version of '$file'."
            return $false
        }
    }

    $stillConflicted = @(& git diff --name-only --diff-filter=U)
    if ($LASTEXITCODE -ne 0 -or $stillConflicted.Count -gt 0) {
        return $false
    }

    Write-Host -f Green "Took the '$targetBranch' version of conflicted ResetToTargetPaths files: $($covered -join ', ')"
    return $true
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

    # Configure git user for the commit
    # Use GitHub Actions bot identity
    Invoke-Block { & git config user.name "github-actions[bot]" }
    Invoke-Block { & git config user.email "41898282+github-actions[bot]@users.noreply.github.com" }

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

    # Build the merge branch.
    #
    # When an open PR already exists for this merge branch, update that branch by merging the
    # source branch into it rather than recreating it from the source branch tip. Recreating the
    # branch produces history that is not a descendant of what was pushed on the previous run --
    # with ResetToTargetPaths the "Reset files to <target>" commit is regenerated with a new SHA
    # every run, so the branch can never fast-forward -- and the push below is rejected as
    # non-fast-forward, leaving the PR silently un-updated from then on. Merging into the existing
    # branch keeps the push a fast-forward and preserves conflict resolutions that were pushed to
    # the PR branch by hand.
    $updatedExistingBranch = $false

    if ($matchingPr -and (RemoteBranchExists $remoteName $mergeBranchName)) {
        Invoke-Block { & git fetch --quiet $remoteName "refs/heads/${mergeBranchName}:refs/remotes/${remoteName}/${mergeBranchName}" }
        Invoke-Block { & git checkout -B $mergeBranchName "refs/remotes/$remoteName/$mergeBranchName" }

        # A merge commit needs an identity. ResetFilesToTargetBranch configures the same one.
        Invoke-Block { & git config user.name "github-actions[bot]" }
        Invoke-Block { & git config user.email "41898282+github-actions[bot]@users.noreply.github.com" }

        # No -X ours/-X theirs: nothing outside ResetToTargetPaths is ever auto-resolved.
        $mergeOutput = & git merge --no-edit "refs/remotes/$remoteName/$MergeFromBranch" 2>&1
        $mergeExitCode = $LASTEXITCODE

        if ($mergeOutput) {
            $mergeOutput | Write-Host
        }

        if ($mergeExitCode -eq 0) {
            $updatedExistingBranch = $true
        }
        elseif ($ResetToTargetPaths -and (TryResolveResetPathConflicts ($ResetToTargetPaths -split ";") $MergeToBranch)) {
            # Every conflicted file was one this script overwrites with the target branch's content
            # anyway, and it has been set to that content. Finish the merge.
            Invoke-Block { & git commit --no-edit }
            $updatedExistingBranch = $true
        }
        else {
            # Abort and fall back to recreating the branch from the source tip. That is what this
            # script did before this branch existed: the push below is rejected as non-fast-forward
            # and the existing PR is left untouched for someone to resolve by hand.
            # Plain call, not Invoke-Block: `git merge --abort` exits non-zero when the merge failed
            # for a reason that left no merge in progress.
            & git merge --abort 2>&1 | Write-Host
            Write-Host -f Yellow "Could not merge $MergeFromBranch into the existing '$mergeBranchName' branch; it needs manual conflict resolution. The existing PR will be left unchanged."
        }
    }

    if (-not $updatedExistingBranch) {
        Invoke-Block { & git checkout -B $mergeBranchName "refs/remotes/$remoteName/$MergeFromBranch" }
    }

    # Reset specified files to target branch if ResetToTargetPaths is configured
    if ($ResetToTargetPaths) {
        $patterns = $ResetToTargetPaths -split ";"
        ResetFilesToTargetBranch $patterns $MergeToBranch
    }

    if ($matchingPr) {
        $prUpdatedSuccess = $false

        if ($PSCmdlet.ShouldProcess("Update remote branch $mergeBranchName on $remoteName")) {
            # Attempt a fast-forward update of the existing PR branch. Capture the output so we can
            # distinguish an expected non-fast-forward rejection -- the PR branch has intentionally
            # diverged (e.g. manual conflict-resolution commits, or a regenerated ResetToTargetPaths
            # commit) and we deliberately do not force-push over it -- from a genuine push failure
            # such as an auth, network, or permissions problem, which must still fail the workflow.
            Write-Host "git push $remoteName ${mergeBranchName}:${mergeBranchName}"
            $pushOutput = & git push $remoteName "${mergeBranchName}:${mergeBranchName}" 2>&1
            $pushExitCode = $LASTEXITCODE
            $pushOutputText = ($pushOutput | Out-String)
            Write-Host $pushOutputText

            if ($pushExitCode -eq 0) {
                $prUpdatedSuccess = $true
            }
            elseif ($pushOutputText -match '(?i)\[rejected\]|non-fast-forward|fetch first|tip of your current branch is behind') {
                # Benign: the existing PR has diverged and cannot be fast-forwarded. Leave the PR
                # untouched and do not fail the job. Reset the exit code so the non-fast-forward
                # rejection from git push does not propagate as the process exit code.
                Write-Host -f Yellow "The existing PR branch '$mergeBranchName' has diverged and cannot be fast-forwarded; leaving the existing PR unchanged."
                $global:LASTEXITCODE = 0
            }
            else {
                throw "Failed to push updates to existing PR branch '$mergeBranchName'. See push output above."
            }
        }
        else {
            $prUpdatedSuccess = $true
        }

        $prMessage = if ($prUpdatedSuccess) {
            "This pull request has been updated.`n`n$committersList"
        } else {
            @"
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
