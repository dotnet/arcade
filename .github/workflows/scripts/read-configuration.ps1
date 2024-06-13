#!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request to merge a head branch into a base branch
.PARAMETER RepoOwner
The GitHub repository owner.
.PARAMETER RepoName
The GitHub repository name.
.PARAMETER HeadBranch
The current branch
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
    $HeadBranch,

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
    try{
        $response = Invoke-WebRequest -Method GET -MaximumRetryCount 3 -Headers $headers `
                "https://raw.githubusercontent.com/$RepoOwner/$RepoName/$ConfigurationFileBranch/$ConfigurationFilePath"
        
        $mergeFlowConfig = ConvertFrom-Json -InputObject $response.Content -AsHashTable
        if ($mergeFlowConfig -eq $null) {
            Write-Warning "Failed to read configuration file from default branch"
            return $null
        }

        if (!$mergeFlowConfig.ContainsKey('merge-flow-configurations')) {
            Write-Host "no merge-flow-configurations found in configuration file from config file branch"
            return $null
        }

        if($mergeFlowConfig['merge-flow-configurations'].ContainsKey($HeadBranch)){
            $config = $mergeFlowConfig['merge-flow-configurations'][$HeadBranch]
            Write-Host "Found Configuration"
            Write-Host $config
            return $config
        }else{
            Write-Warning "There were no configuration found in default branch for $HeadBranch"
        }
    }catch{
        Write-Warning "Failed to fetch and process configuration file from default branch"
    }

    return $null
}

# fetch configuration
$configuration = GetConfiguration

if ($configuration -ne $null) {
    if($configuration.ContainsKey('MergeToBranch')){
        $MergeToBranch = $configuration['MergeToBranch']
    }else{
        Write-Warning "Configuration provided is incorrect and does not contain the required parameter: MergeToBranch"
        exit 0
    }

    $ExtraSwitches = "";
    if($configuration.ContainsKey('ExtraSwitches')){
        $ExtraSwitches = $configuration['ExtraSwitches']
    }

    "mergeSwitchArguments=$ExtraSwitches" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "mergeToBranch=$MergeToBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "configurationFound=$true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}

exit 0