<#
.SYNOPSIS
Prepares data for migration, migrates and verifies your DARC subcriptions and default channels.

.DESCRIPTION
This script runs in 3 modes:
    1. Initialization - creates json file which describes DARC migration and disables targeting subscriptions
        for your repository and branch.
    2. Migration - using json file generated during Initializatin, this script removes default channels
        and targeting subscriptions for your repository and branch. Then it recreates them under
        a new branch.
    3. Verification - Compares default channels and targeting subscriptions from json file against current
        state in DARC.

.PARAMETER Repository
Mandatory short name of GitHub repository (e.g. dotnet/runtime or dotnet/wpf). This short name is transformed to
public and internal repository (e.g. for dotnet/runtime https://github.com/dotnet/runtime and
https://dev.azure.com/dnceng/internal/_git/dotnet-runtime).

.PARAMETER NewBranch
Optional new name of branch, defaults to 'main'.

.PARAMETER OldBranch
Optional old name of branch, defaults to 'master'.

.PARAMETER Migrate
Mandatory json file path used for DARC migration.

.PARAMETER Verify
Mandatory json file path used for DARC validation.

.PARAMETER DryRun
When specified then no DARC updates are executed, but only logged.

.EXAMPLE
1. For initilization execute:
./m2m-dotnet.ps1 -Repository dotnet/m2m-renaming-test-1
or you can additionaly specify branch names:
./m2m-dotnet.ps1 -Repository dotnet/m2m-renaming-test-1  -OldBranch "master" -NewBranch "main"

This generates data file m2m-dotnet_[timestamp].json and disables all targeting subscriptions.

2. For migration execute:
.\m2m-dotnet.ps1  -Migrate m2m-dotnet_[timestamp].json

3. For verification execute:
.\m2m-dotnet.ps1  -Verify m2m-dotnet_[timestamp].json

#>

[CmdletBinding()]
param (
    [Parameter(ParameterSetName = 'Initialize', Mandatory = $true)]
    [string]$Repository,
    [Parameter(ParameterSetName = 'Initialize')]
    [string]$NewBranch = "main",
    [Parameter(ParameterSetName = 'Initialize')]
    [string]$OldBranch = "master",
    [Parameter(ParameterSetName = 'Migrate', Mandatory = $true)]
    [string]$Migrate,
    [Parameter(ParameterSetName = 'Verify', Mandatory = $true)]
    [string]$Verify,
    [Parameter(Mandatory = $false)]
    [switch]$DryRun = $false
)


Class DarcExecutor {
    [bool]$DryRun = $false

    [object[]] ParseIgnoreChecks([string] $line) {
        $ignoreChecks = @()
        if ($line -match "ignoreChecks\s*=\s*\[\s*([^\]]+)\s*\]") {
            $ignoreChecksValuesMatches = [regex]::matches($matches[1], "`"([^`"]+)`"")
            ForEach ($check in $ignoreChecksValuesMatches) {
                $ignoreChecks += $check.Groups[1].Value
            }
        }

        return $ignoreChecks
    }

    [object[]] ParseMergePolicies([string] $line) {
        $line = $line -replace "ignoreChecks\s*=\s*\[\s*[^\]]+\s*\]", ""
        $policies = $line -split "\s+" | Where-Object { $_ }
        return $policies
    }

    [object[]] ParseSubscriptions([string] $output) {
        $darcOutputLines = $output.Split([Environment]::NewLine)
        $list = @()
        $processingMergePolicies = $false
        $batchable = $fromRepo = $fromChannel = $updateFrequency = $enabled = $mergePolicies = $null
        For ($i = 0; $i -le $darcOutputLines.Length; $i++) {
            $line = $darcOutputLines[$i]

            if ($line -match "([^\s]+)\s+\(([^\)]+)\)\s+==>\s+'([^']+)'\s+\('([^\)]+)'\)") {
                if ($i -ne 0) {
                    $list += @{fromRepo = $fromRepo; fromChannel = $fromChannel; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; ignoreChecks = @($this.ParseIgnoreChecks($mergePolicies)); mergePolicies = @($this.ParseMergePolicies($mergePolicies)) };
                }

                $updateFrequency = $enabled = $batchable = $mergePolicies = ""

                $fromRepo = $matches[1]
                $fromChannel = $matches[2]
            }
            elseif ($line -match "^\s+\-\s+([^:]+):\s*(.*)") {
                $processingMergePolicies = $false
                if ($matches[1] -eq "Update Frequency") {
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
            $list += @{fromRepo = $fromRepo; fromChannel = $fromChannel; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; ignoreChecks = @($this.ParseIgnoreChecks($mergePolicies)); mergePolicies = @($this.ParseMergePolicies($mergePolicies)) };
        }

        return $list
    }

    [object[]] GetSubscriptions([string]$repo, [string]$branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $output = $this.Execute($arguments, $false)
        $subscriptions = @($this.ParseSubscriptions($output))
        return $subscriptions
    }


    [void]AddSubscription($repo, $branch, $item) {
        $arguments = @("add-subscription", "--channel", $item.fromChannel, "--source-repo", $item.fromRepo, "--target-repo", $repo, "--update-frequency", $item.updateFrequency, "--target-branch", $branch, "--no-trigger", "-q")

        if ($item.mergePolicies -contains "Standard") {
            $arguments += "--standard-automerge"
        }
        if ($item.mergePolicies -like "NoRequestedChanges") {
            $arguments += "--no-requested-changes"
        }
        if ($item.mergePolicies -like "NoExtraCommits") {
            $arguments += "--no-extra-commits"
        }
        if ($item.mergePolicies -like "AllChecksSuccessful") {
            $arguments += "--all-checks-passed"
        }
        if ($item.ignoreChecks.length -gt 0) {
            $arguments += "--ignore-checks"
            $arguments += $item.ignoreChecks -join ","
        }
        if ($item.batchable -eq "True") {
            $arguments += " --batchable"
        }

        $output = $this.Execute($arguments, $true)

        if ($output -match "Successfully created new subscription with id '([^']+)'.") {
            $id = $matches[1]
            if ($item.enabled -eq [bool]::FalseString) {
                $this.DisableSubscription($id)
            }
        }
        else {
            Write-Host("    WARNING: {0}" -f $output)
        }
    }

    [void]DeleteSubscriptions($repo, $branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $output = $this.Execute($arguments, $false)
        if (-not ($output -match "^No subscriptions found matching the specified criteria.")) {
            Write-Host ("Deleting subscriptions for {0} {1}" -f $repo, $branch)
            $arguments = @("delete-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch, "-q")
            $output = $this.Execute($arguments, $true)

            if ($output -notmatch ".*done") {
                Write-Host ("   WARNING: {0}" -f $output)
            }
        }
    }

    [void]CreateDefaultChannel($repo, $branch, $channel) {
        Write-Host ("Creating default channel {2} for branch {0} {1}" -f $repo, $branch, $channel)
        $arguments = @("add-default-channel", "--repo", $repo, "--branch", $branch, "--channel", $channel, "-q")
        $this.Execute($arguments, $true)
    }

    [void]DisableSubscription ([string] $id) {
        Write-Host ("Disabling subscription {0}" -f $id)
        $arguments = @("subscription-status", "--id", $id, "-d", "-q")
        $output = $this.Execute($arguments, $true)

        if ($output -notmatch ".*done") {
            Write-Host ("   WARNING: {0}" -f $output)
        }
    }

    [string[]]GetTargetSubscriptionIds ([string] $repo, [string] $branch) {
        $arguments = @("get-subscriptions", "--exact", "--target-repo", $repo, "--target-branch", $branch)
        $ids = $this.Execute($arguments, $false) | Select-String -AllMatches -Pattern "\s+-\s+Id:\s+([^\s]+)" |  ForEach-Object { $_.Matches } | Foreach-Object { $_.Groups[1].Value }
        return $ids
    }

    [void]DisableTargetSubscriptions ([string] $repo, [string] $branch) {
        Write-Host "Disabling targeting subscriptions for $repo ($branch)"

        $ids = $this.GetTargetSubscriptionIds($repo, $branch)
        ForEach ($id in $ids) {
            $this.DisableSubscription($id)
        }
    }

    [Hashtable[]]GetDefaultChannels ([string] $repo, [string] $branch) {
        $arguments = @("get-default-channels", "--source-repo", $repo, "--branch", $branch)
        $output = $this.Execute($arguments, $true)
        $records = @($output | Select-String -AllMatches -Pattern "\((\d+)\)\s+$repo\s+@\s+$branch\s+->\s+(.*)\b" |  ForEach-Object { $_.Matches } |  ForEach-Object { @{id = $_.Groups[1].Value; channel = $_.Groups[2].Value } })
        return $records
    }

    [void]DeleteDefaultChannel([string] $id) {
        Write-Host ("Deleting default channel {0}" -f $id)
        $arguments = @("delete-default-channel", "--id", $id)
        $this.Execute($arguments, $true)
    }

    [void]DeleteDefaultChannels([string] $repo, [string] $branch) {
        $channels = @($this.GetDefaultChannels($repo, $branch))
        ForEach ($item in $channels) {
            $this.DeleteDefaultChannel($item.id)
        }
    }

    [Hashtable]GetRepoConfig([string] $repo, [string] $newBranch, [string] $oldBranch) {
        $defaultChannels = @($this.GetDefaultChannels($repo, $oldBranch) | ForEach-Object { $_.channel })
        $subscriptions = @($this.GetSubscriptions($repo, $oldBranch))
        $config = @{repo = $repo; newBranch = $newBranch; oldBranch = $oldBranch; defaultChannels = $defaultChannels; subscriptions = $subscriptions; }
        return $config
    }

    [void]MigrateRepo([PSCustomObject]$config) {
        Write-Host (">>>Migrating repository {0} {1} ==> {2}..." -f $config.repo, $config.oldBranch, $config.newBranch)

        $this.DeleteDefaultChannels($config.repo, $config.oldBranch)
        ForEach ($channel in $config.defaultChannels) {
            $this.CreateDefaultChannel($config.repo, $config.newBranch, $channel)
        }

        $this.DeleteSubscriptions($config.repo, $config.oldBranch)
        $this.DeleteSubscriptions($config.repo, $config.newBranch)

        Write-Host ("Adding subscriptions")
        ForEach ($item in $config.subscriptions) {
            $this.AddSubscription($config.repo, $config.newBranch, $item)
        }
    }

    [void]VerifyRepo([PSCustomObject]$config) {
        Write-Host (">>>Verifying repository {0} {1} ==> {2}..." -f $config.repo, $config.oldBranch, $config.newBranch)
        if ($this.GetDefaultChannels($config.repo, $config.oldBranch).length -ne 0) {
            throw("Default channels for old branch haven't been removed.")
        }
        if ($this.GetTargetSubscriptionIds($config.repo, $config.oldBranch).length -ne 0) {
            throw("Subscriptions for old branch haven't been removed.")
        }

        $actualConfig = $this.GetRepoConfig($config.repo, $config.oldBranch, $config.newBranch)
        if ($actualConfig.defaultChannels.length -ne $config.defaultChannels.length) {
            throw("Subscriptions for old branch haven't been removed.")
        }

        $expectedDefaultChannels = ConvertTo-Json($actualConfig.defaultChannels | Sort-Object)
        $actualDefaultChannels = ConvertTo-Json($config.defaultChannels | Sort-Object)
        if($expectedDefaultChannels -ne $actualDefaultChannels) {
            throw("Expected default channels {0} don't match actual {1}." -f $actualDefaultChannels, $actualDefaultChannels)
        }

        $expectedSubscriptions = ConvertTo-Json($actualConfig.subscriptions | Sort-Object -Property "fromRepo")
        $actualSubscriptions = ConvertTo-Json($config.subscriptions | Sort-Object -Property "fromRepo")
        if($expectedSubscriptions -ne $actualSubscriptions) {
            throw("Expected subscriptions {0} don't match actual {1}." -f $expectedSubscriptions, $actualSubscriptions)
        }

        Write-Host ("Validation of {0} passed" -f $config.repo)
    }

    [string]Execute ([string[]] $arguments, [bool]$exitCodeCheck) {
        if ($this.DryRun -and ($arguments[0] -ne "get-default-channels") -and ($arguments[0] -ne "get-subscriptions")) {
            Write-Host (">>> darc {0}" -f ($arguments -join " "))
            return "done"
        }
        else {
            $output = (&"darc"  $arguments | Out-String)
            if ($exitCodeCheck -and $LASTEXITCODE -ne 0) {
                throw ("    Error with status code {0}: {1}" -f $LASTEXITCODE, $output)
            }
            return $output
        }
    }
}

function  InitDarcConfigurationAndDisableSubscriptions {
    param (
        [DarcExecutor] $darc
    )
    $configFile = "m2m-dotnet_{0:ddMMyyyy_HHmmss}.json" -f (get-date)
    $internalRepo = "https://dev.azure.com/dnceng/internal/_git/{0}" -f ($Repository -replace "/", "-")
    $publicRepo = "https://github.com/{0}" -f $Repository

    Write-Host ("Creating configuration for repository {0} {1} ==> {2}..." -f $publicRepo, $OldBranch, $NewBranch)
    $configPublic = $darc.GetRepoConfig($publicRepo, $NewBranch, $OldBranch)

    Write-Host ("Creating configuration for repository {0} {1} ==> {2}..." -f $internalRepo, $OldBranch, $NewBranch)
    $configInternal = $darc.GetRepoConfig($internalRepo, $NewBranch, $OldBranch)

    $configs = @($configPublic, $configInternal)
    ConvertTo-Json $configs -Depth 4 | Out-File -FilePath $configFile
    Write-Host ("Configuration has been saved as {0}" -f $configFile)

    $darc.DisableTargetSubscriptions($publicRepo, $OldBranch)
    $darc.DisableTargetSubscriptions($internalRepo, $OldBranch)
}
function MigrateDarc {
    param (
        [DarcExecutor]$darc
    )

    $configs = Get-Content -Raw -Path $Migrate | ConvertFrom-Json
    ForEach ($config in $configs) {
        $darc.MigrateRepo($config)
    }
}
function VerifyDarc {
    param (
        [DarcExecutor]$darc
    )

    $configs = Get-Content -Raw -Path $Verify | ConvertFrom-Json
    ForEach ($config in $configs) {
        $darc.VerifyRepo($config)
    }
}

$ErrorActionPreference = 'Stop'
$darc = [DarcExecutor]::new()
$darc.DryRun = $DryRun

if ($PSCmdlet.ParameterSetName -eq "Initialize") {
    InitDarcConfigurationAndDisableSubscriptions -darc $darc
}
elseif ($PSCmdlet.ParameterSetName -eq "Migrate") {
    MigrateDarc -darc $darc
}
elseif ($PSCmdlet.ParameterSetName -eq "Verify") {
    VerifyDarc -darc $darc
}
