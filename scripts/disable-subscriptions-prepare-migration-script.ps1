<#
.SYNOPSIS
This script doesn't update Maestro directly and runs only darc read commands. It generates two Powershell
scripts with a sequence of darc commands which can be first reviewed and then executed.

Generated scripts:
    1. Script which disables all Maestro subscriptions where specified internal or GitHub repository is a target
    2. Script which
        * takes all default channels targeting master in specified internal or GitHub repository
          and re-creates them for main
        * takes all subscriptions targeting master in specified internal or GitHub repository
          and re-creates them for main
        Note: subscriptions sourcing specified internal or GitHub repository aren't recreated because branch is
              defined on a channel, not on subscription (this is done in the first step).

.PARAMETER Repository
Mandatory short name of GitHub repository (e.g. dotnet/runtime or dotnet/wpf)

.PARAMETER NewBranch
Optional new name of branch, defaults to 'main'.

.PARAMETER OldBranch
Optional old name of branch, defaults to 'master'.
#>

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$Repository,
    [string]$NewBranch = "main",
    [string]$OldBranch = "master"
)

$SPLIT_LINE = "# " + "-" * 80

function BuildDarcCreateSubscruptionsCommands {
    param (
        $items,
        $branch
    )

    $commands = @()
    ForEach ($item in $items) {
        $cmd = ""
        if ($item.enabled -ne "True") {
            $cmd = "`$ret="
        }
        $cmd += "darc add-subscription --channel `"$($item.fromChannel)`" --source-repo `"$($item.fromRepo)`" --target-repo `"$($item.toRepo)`" --update-frequency `"$($item.updateFrequency)`" --target-branch `"$branch`" --no-trigger -q"
        if ($item.mergePolicies -like "*Standard*") {
            $cmd += " --standard-automerge"
        }
        if ($item.mergePolicies -like "*NoRequestedChanges*") {
            $cmd += " --no-requested-changes"
        }
        if ($item.mergePolicies -like "*NoExtraCommits*") {
            $cmd += " --no-extra-commits"
        }
        if ($item.mergePolicies -like "*AllChecksSuccessful*") {
            $cmd += " --all-checks-passed"
            $ignoreChecksMatch = [regex]::match($item.mergePolicies, "ignoreChecks\s*=\s*\[\s*([^\]]+)\s*\]")
            if ($ignoreChecksMatch.Success) {
                $cmd += " --ignore-checks `""
                $ignoreChecksValuesMatches = [regex]::matches($ignoreChecksMatch.Groups[1].Value, "`"([^`"]+)`"")
                $first = $True
                ForEach ($check in $ignoreChecksValuesMatches) {
                    if (-not $first) {
                        $cmd += ","
                    }
                    $first = $false
                    $cmd += $check.Groups[1].Value
                }
                $cmd += "`""
            }
        }
        if ($item.batchable -eq "True") {
            $cmd += " --batchable"
        }

        $commands += $cmd

        if ($item.enabled -ne "True") {
            $commands += "Write-Output `$ret"
            $commands += "`$id=[regex]::match(`$ret,`"Successfully created new subscription with id '([^']+)'.`").Groups[1].Value"
            $commands += "darc subscription-status --id `$id -d -q"
        }
    }

    return $commands
}

function ParseDarcOutput {
    param (
        $darcOutputLines
    )

    $list = @()
    For ($i = 0; $i -le $darcOutputLines.Length; $i++) {
        $line = $darcOutputLines[$i]
        $headerMatch = [regex]::match($line, "([^\s]+)\s+\(([^\)]+)\)\s+==>\s+'([^']+)'\s+\('([^\)]+)'\)")

        if ($headerMatch.Success) {
            if ($i -ne 0) {
                $list += @{fromRepo = $fromRepo; toRepo = $toRepo; fromChannel = $fromChannel; toBranch = $toBranch; id = $id; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; mergePolicies = $mergePolicies }
            }

            $id = ""
            $updateFrequency = ""
            $enabled = ""
            $batchable = ""
            $mergePolicies = ""

            $fromRepo = $headerMatch.Groups[1].Value
            $fromChannel = $headerMatch.Groups[2].Value
            $toRepo = $headerMatch.Groups[3].Value
            $toBranch = $headerMatch.Groups[4].Value
        }

        $idMatch = [regex]::match($line, "\s+\-\s+Id:\s+(.*)")
        if ($idMatch.Success) {
            $id = $idMatch.Groups[1].Value
            continue
        }
        $updateFrequencyMatch = [regex]::match($line, "\s+\-\s+Update Frequency:\s+(.*)")
        if ($updateFrequencyMatch.Success) {
            $updateFrequency = $updateFrequencyMatch.Groups[1].Value
            continue
        }
        $enabledMatch = [regex]::match($line, "\s+\-\s+Enabled:\s+(.*)")
        if ($enabledMatch.Success) {
            $enabled = $enabledMatch.Groups[1].Value
            continue
        }
        $batchableMatch = [regex]::match($line, "\s+\-\s+Batchable:\s+(.*)")
        if ($batchableMatch.Success) {
            $batchable = $batchableMatch.Groups[1].Value
            continue
        }
        $mergePoliciesMatch = [regex]::match($line, "\s+\-\s+Merge Policies:(.*)")
        if ($mergePoliciesMatch.Success) {
            $mergePolicies = $mergePoliciesMatch.Groups[1].Value
            continue
        }
        $lastBuildMatch = [regex]::match($line, "\s+\-\s+Last Build:(.*)")
        if (-not ($mergePoliciesMatch.Success -or $batchableMatch.Success -or $enabledMatch.Success -or $updateFrequencyMatch.Success -or $idMatch.Success -or $headerMatch.Success -or $lastBuildMatch.Success)) {
            $mergePolicies += $line
        }
    }
    if ($null -ne $fromRepo) {
        $list += @{fromRepo = $fromRepo; toRepo = $toRepo; fromChannel = $fromChannel; toBranch = $toBranch; id = $id; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; mergePolicies = $mergePolicies };
    }
    return $list
}

function  GenerateMigrationDarcScript {
    param (
        [string]$repo,
        [string]$newBranch,
        [string]$oldBranch
    )

    $migrationCommands = @()
    $migrationCommands += "# Recreate default channels for $repo"
    $migrationCommands += $SPLIT_LINE
    $items = @(darc get-default-channels --source-repo "$repo" --branch "$oldBranch" | Select-String -Pattern "\((\d+)\)\s+$repo\s+@\s+$oldBranch\s+->\s+(.*)" | ForEach-Object { [ordered]@{id = $_.matches.Groups[1].Value; chanel = $_.matches.Groups[2].Value } })

    ForEach ($item in $items) {
        $migrationCommands += "darc delete-default-channel --id `"$($item.id)`""
        $migrationCommands += "darc add-default-channel --repo `"$repo`" --branch $newBranch --channel `"$($item.chanel)`""
    }

    $darcOutputLines = (darc get-subscriptions --exact --target-repo "$repo" --target-branch "$oldBranch")
    $itemsTarget = ParseDarcOutput $darcOutputLines
    $commandsTarget = BuildDarcCreateSubscruptionsCommands $itemsTarget $newBranch

    $migrationCommands += "# Recreate target subscriptions for $repo"
    $migrationCommands += $SPLIT_LINE
    $migrationCommands += "darc delete-subscriptions --exact --target-repo `"$repo`" --target-branch `"$oldBranch`" -q"
    $migrationCommands += $commandsTarget

    return $migrationCommands
}

function  GenerateDisableDarcScript {
    param (
        [string] $repo,
        [string] $newBranch,
        [string] $oldBranch
    )

    $disabledCommands = @()
    $disabledCommands += "# Disable target subscriptions for $repo"
    $disabledCommands += $SPLIT_LINE
    $lines = (darc get-subscriptions --exact --target-repo "$repo" --target-branch "$oldBranch")
    $ids = $lines | Select-String -Pattern "\s+-\s+Id:\s+([^\s]+)" | ForEach-Object { $_.matches.Groups[1].Value }
    ForEach ($id in $ids) {
        $disabledCommands += "darc subscription-status --id `"$id`" -d -q"
    }

    return $disabledCommands
}

function  GenerateDarcScripts {
    param (
        [string]$repo,
        [string]$newBranch,
        [string]$oldBranch
    )
    $migrationFile = "rename-branch-in-maestro.ps1"
    $disableFile = "disable-subscriptions-in-maestro.ps1"

    $internalRepo = "https://dev.azure.com/dnceng/internal/_git/{0}" -f ($repo -replace "/", "-")
    Write-Output ("Generating darc scripts for repository {0} {1} ==> {2}..." -f $internalRepo, $oldBranch, $newBranch)

    $migrationCommands = GenerateMigrationDarcScript $internalRepo $newBranch $oldBranch
    $migrationCommands | Out-File -FilePath $migrationFile
    $disableCommands = GenerateDisableDarcScript $internalRepo $newBranch $oldBranch
    $disableCommands |  Out-File -FilePath $disableFile

    $publicRepo = "https://github.com/{0}" -f $repo
    Write-Output ("Generating darc scripts for repository {0} {1} ==> {2}..." -f $publicRepo, $oldBranch, $newBranch)

    $migrationCommands = GenerateMigrationDarcScript $publicRepo $newBranch $oldBranch
    $migrationCommands | Out-File -Append -FilePath $migrationFile
    $disableCommands = GenerateDisableDarcScript $publicRepo $newBranch $oldBranch
    $disableCommands |  Out-File -Append -FilePath $disableFile

    Write-Output ("Files {0} and {1} were generated." -f $migrationFile, $disableFile)
}


GenerateDarcScripts $Repository $NewBranch $OldBranch
