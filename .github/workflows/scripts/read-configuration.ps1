#!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request to merge a head branch into a base branch
.PARAMETER RepoOwner
The GitHub repository owner.
.PARAMETER RepoName
The GitHub repository name.
.PARAMETER MergeFromBranch
The source branch to merge from. This is the key used to look up the merge flow configuration.
.PARAMETER ConfigurationFileBranch
The ConfigurationFileBranch is the branch where the configuration file is stored.
.PARAMETER ConfigurationFilePath
The ConfigurationFilePath is the path to the configuration file.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Alias('o')]
    [Parameter(Mandatory = $true)]
    $RepoOwner,

    [Alias('n')]
    [Parameter(Mandatory = $true)]
    $RepoName,

    [Alias('h')]
    [Parameter(Mandatory = $true)]
    $MergeFromBranch,

    [Alias('d')]
    [Parameter(Mandatory = $true)]
    $ConfigurationFileBranch,

    [Alias('c')]
    [Parameter(Mandatory = $true)]
    $ConfigurationFilePath
)

Set-StrictMode -Version 1
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12


$stringToken = $Env:GH_TOKEN;

$headers = @{
    Authorization = "bearer $stringToken"
}

function GetConfiguration {
    # Read the configuration file from provided branch
    $urlToConfigurationFile = "https://raw.githubusercontent.com/$RepoOwner/$RepoName/$ConfigurationFileBranch/$ConfigurationFilePath"
    Write-Host "Fetching configuration file from $urlToConfigurationFile"

    try{
        $response = Invoke-WebRequest -UseBasicParsing -Method GET -MaximumRetryCount 3 -Headers $headers `
                $urlToConfigurationFile
        
        $mergeFlowConfig = ConvertFrom-Json -InputObject $response.Content -AsHashTable
        if ($mergeFlowConfig -eq $null) {
            Write-Warning "Failed to read configuration file"
            return $null
        }

        if (!$mergeFlowConfig.ContainsKey('merge-flow-configurations')) {
            Write-Host "No merge-flow-configurations found in configuration file"
            return $null
        }

        if($mergeFlowConfig['merge-flow-configurations'].ContainsKey($MergeFromBranch)){
            $config = $mergeFlowConfig['merge-flow-configurations'][$MergeFromBranch]
            Write-Host "Found Configuration"
            Write-Host $config
            return $config
        }else{
            Write-Host "There was no configuration found for $MergeFromBranch"
        }
    }catch{
        Write-Warning "Failed to fetch and process configuration file"
    }

    return $null
}

# fetch configuration
$configuration = GetConfiguration

if ($configuration -ne $null) {
    # A matching configuration entry always carries the merge policy, but MergeToBranch is optional:
    # the caller of the workflow may supply the merge target instead. 'configurationFound' keeps its
    # original meaning of a complete, self-contained entry so that copies of the workflow pinned to
    # an older ref keep behaving exactly as before, while 'policyFound' reports the weaker condition
    # that an entry matched at all.
    $MergeToBranch = "";
    if($configuration.ContainsKey('MergeToBranch')){
        $MergeToBranch = $configuration['MergeToBranch']
    }else{
        Write-Host "Configuration does not contain MergeToBranch. The merge target must be supplied by the caller."
    }

    $ExtraSwitches = "";
    if($configuration.ContainsKey('ExtraSwitches')){
        $ExtraSwitches = $configuration['ExtraSwitches']
    }

    $ResetToTargetPaths = "";
    if($configuration.ContainsKey('ResetToTargetPaths')){
        # Convert array to semicolon-separated string for output
        $ResetToTargetPaths = $configuration['ResetToTargetPaths'] -join ";"
    }

    "mergeSwitchArguments=$ExtraSwitches" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "mergeToBranch=$MergeToBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "resetToTargetPaths=$ResetToTargetPaths" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "policyFound=$true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    if ($MergeToBranch) {
        "configurationFound=$true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}

exit 0