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

$ErrorActionPreference = 'Stop'
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
            if ($item.mergePolicies -match "ignoreChecks\s*=\s*\[\s*([^\]]+)\s*\]") {
                $cmd += " --ignore-checks `""
                $ignoreChecksValuesMatches = [regex]::matches($matches[1], "`"([^`"]+)`"")
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
            $commands += "if(`$ret -match `"Successfully created new subscription with id '([^']+)'.`") {"
            $commands += "  darc subscription-status --id `"`$matches[1]`" -d -q"
            $commands += "}"
        }
    }

    return $commands
}

function ParseDarcOutput {
    param (
        $darcOutputLines
    )

    $list = @()
    $processingMergePolicies = $false
    For ($i = 0; $i -le $darcOutputLines.Length; $i++) {
        $line = $darcOutputLines[$i]

        if ($line -match "([^\s]+)\s+\(([^\)]+)\)\s+==>\s+'([^']+)'\s+\('([^\)]+)'\)") {
            if ($i -ne 0) {
                $list += @{fromRepo = $fromRepo; toRepo = $toRepo; fromChannel = $fromChannel; toBranch = $toBranch; id = $id; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; mergePolicies = $mergePolicies }
            }

            $id = $updateFrequency = $enabled = $batchable = $mergePolicies = ""

            $fromRepo = $matches[1]
            $fromChannel = $matches[2]
            $toRepo = $matches[3]
            $toBranch = $matches[4]
        }
        elseif ($line -match "^\s+\-\s+([^:]+):\s*(.*)") {
            $processingMergePolicies = $false
            if ($matches[1] -eq "Id") {
                $id = $matches[2]
            }
            elseif ($matches[1] -eq "Update Frequency") {
                $updateFrequency = $matches[2]
            }
            elseif ($matches[1] -eq "Enabled") {
                $enabled = $matches[2]
            }
            elseif ($matches[1] -eq "Batchable") {
                $batchable = $matches[2]
            }
            elseif ($matches[1] -eq "Merge Policies") {
                $mergePolicies = $matches[2]
                $processingMergePolicies = $true
            }
        }
        elseif ($processingMergePolicies) {
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
    $migrationCommands += "# Recreate default channels for $repo ($oldBranch)"
    $migrationCommands += $SPLIT_LINE
    $items = @(darc get-default-channels --source-repo "$repo" --branch "$oldBranch" | Select-String -Pattern "\((\d+)\)\s+$repo\s+@\s+$oldBranch\s+->\s+(.*)" | ForEach-Object { [ordered]@{id = $_.matches.Groups[1].Value; chanel = $_.matches.Groups[2].Value } })

    ForEach ($item in $items) {
        $migrationCommands += "darc delete-default-channel --id `"$($item.id)`""
        $migrationCommands += "darc add-default-channel --repo `"$repo`" --branch `"$newBranch`" --channel `"$($item.chanel)`""
    }

    $darcOutputLines = (darc get-subscriptions --exact --target-repo "$repo" --target-branch "$oldBranch")
    $itemsTarget = ParseDarcOutput $darcOutputLines
    $commandsTarget = BuildDarcCreateSubscruptionsCommands $itemsTarget $newBranch

    $migrationCommands += "# Recreate targeting subscriptions for $repo ($oldBranch)"
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
    $disabledCommands += "# Disable targeting subscriptions for $repo ($oldBranch)"
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

    $disableCommands = $migrationCommands = @("`$ErrorActionPreference = 'Stop'")

    $migrationCommands += GenerateMigrationDarcScript $internalRepo $newBranch $oldBranch
    $migrationCommands | Out-File -FilePath $migrationFile

    $disableCommands += GenerateDisableDarcScript $internalRepo $newBranch $oldBranch
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
