function BuildDarcCreateSubscruptionsCommands {
    param (
        $items,
        $branch
    )

    $commands = @()
    ForEach ($item in $items) {
        $cmd = "`$ret=darc add-subscription --channel `"$($item.fromChannel)`" --source-repo `"$($item.fromRepo)`" --target-repo `"$($item.toRepo)`" --update-frequency `"$($item.updateFrequency)`" --target-branch `"$branch`" --no-trigger -q"
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
                ForEach ($check in $ignoreChecksValuesMatches) {
                    $cmd += $check.Groups[1].Value + ","
                }
                $cmd += "`""
            }
        }
        if ($item.batchable -eq "True") {
            $cmd += " --batchable"
        }

        $commands += $cmd
        $cmd = "Write-Output `$ret"
        $commands += $cmd

        if ($item.enabled -ne "True") {
            $cmd = "`$id=[regex]::match(`$ret,`"Successfully created new subscription with id '([^']+)'.`").Groups[1].Value" 
            $commands += $cmd
            $cmd = "darc subscription-status --id `$id -d -q"
            $commands += $cmd
        }
    }

    return $commands
}

function ParseDarcOutput {
    param (
        $lines
    )
	
    $list = @()
    For ($i = 0; $i -le $lines.Length; $i++) {
        $line = $lines[$i]	
        $headerMatch = [regex]::match($line, "([^\s]+)\s+\(([^\)]+)\)\s+==>\s+'([^']+)'\s+\('([^\)]+)'\)")

        if ($headerMatch.Success) {	
            if ($i -ne 0) {
                $list += @{fromRepo = $fromRepo; toRepo = $toRepo; fromChannel = $fromChannel; toBranch = $toBranch; id = $id; updateFrequency = $updateFrequency; enabled = $enabled; batchable = $batchable; mergePolicies = $mergePolicies };
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
        }
        $updateFrequencyMatch = [regex]::match($line, "\s+\-\s+Update Frequency:\s+(.*)")
        if ($updateFrequencyMatch.Success) {
            $updateFrequency = $updateFrequencyMatch.Groups[1].Value
        }
        $enabledMatch = [regex]::match($line, "\s+\-\s+Enabled:\s+(.*)")
        if ($enabledMatch.Success) {
            $enabled = $enabledMatch.Groups[1].Value
        }
        $batchableMatch = [regex]::match($line, "\s+\-\s+Batchable:\s+(.*)")
        if ($batchableMatch.Success) {
            $batchable = $batchableMatch.Groups[1].Value
        }
        $mergePoliciesMatch = [regex]::match($line, "\s+\-\s+Merge Policies:(.*)")
        if ($mergePoliciesMatch.Success) {
            $mergePolicies = $mergePoliciesMatch.Groups[1].Value
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
    $migrationCommands += "#-----------------------------------"
    $items = @(darc get-default-channels --source-repo "$repo" --branch "$oldBranch" | Select-String -Pattern "\((\d+)\)\s+$repo\s+@\s+$oldBranch\s+->\s+(.*)" | ForEach-Object { [ordered]@{id = $_.matches.Groups[1].Value; chanel = $_.matches.Groups[2].Value } })
    ForEach ($item in $items) {	
        $migrationCommands += "darc delete-default-channel --id `"$($item.id)`""
        $migrationCommands += "darc add-default-channel --repo `"$repo`" --branch $newBranch --channel `"$($item.chanel)`""	
    }
    

    $linesTarget = (darc get-subscriptions --target-repo $repo)
    $itemsTarget = ParseDarcOutput $linesTarget
    $commandsTarget = BuildDarcCreateSubscruptionsCommands $itemsTarget $newBranch

    $linesSource = (darc get-subscriptions --source-repo $repo)
    $itemsSource = ParseDarcOutput $linesSource
    $commandsSource = BuildDarcCreateSubscruptionsCommands $itemsSource $newBranch

    $migrationCommands += "# Recreate target subscriptions for $repo"
    $migrationCommands += "#-----------------------------------"
    $migrationCommands += "darc delete-subscriptions --target-repo `"$repo`" -q"
    $migrationCommands += $commandsTarget

    $migrationCommands += "# Recreate source subscriptions for $repo"
    $migrationCommands += "#-----------------------------------"
    $migrationCommands += "darc delete-subscriptions --source-repo `"$repo`" -q"
    $migrationCommands += $commandsSource

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
    $disabledCommands += "#-----------------------------------"
    $lines = (darc get-subscriptions --target-repo "$repo")
    $ids = $lines | Select-String -Pattern "\s+-\s+Id:\s+([^\s]+)" | ForEach-Object { $_.matches.Groups[1].Value }
    ForEach ($id in $ids) {	    
        $disabledCommands += "darc subscription-status --id $id -d -q"
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

    $fullRepo = "https://dev.azure.com/dnceng/internal/_git/dotnet-" + $repo
    Write-Output ("Generating darc scripts for repository {0} {1} ==> {2}..." -f $fullRepo, $oldBranch, $newBranch)

    $migrationCommands = GenerateMigrationDarcScript $fullRepo $newBranch $oldBranch
    $migrationCommands | Out-File -FilePath $migrationFile
    $disableCommands = GenerateDisableDarcScript $fullRepo $newBranch $oldBranch
    $disableCommands |  Out-File -FilePath $disableFile

    $fullRepo = "https://github.com/dotnet/" + $repo
    Write-Output ("Generating darc scripts for repository {0} {1} ==> {2}..." -f $fullRepo, $oldBranch, $newBranch)

    $migrationCommands = GenerateMigrationDarcScript $fullRepo $newBranch $oldBranch
    $migrationCommands | Out-File -Append -FilePath $migrationFile
    $disableCommands = GenerateDisableDarcScript $fullRepo $newBranch $oldBranch
    $disableCommands |  Out-File -Append -FilePath $disableFile

    Write-Output ("Files {0} and {1} were generated." -f $migrationFile, $disableFile)
}

if ($args.Count -gt 1) {
    GenerateDarcScripts $args[0] $args[1] $args[2]
}
else {
    GenerateDarcScripts $args[0] "main" "master"
}