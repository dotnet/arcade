#!/usr/bin/env pwsh -c

<#
.DESCRIPTION
Creates a GitHub pull request to merge a head branch into a base branch
.PARAMETER RepoOwner
The GitHub repository owner.
.PARAMETER RepoName
The GitHub repository name.
.PARAMETER MergeFromBranch
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

    try {
        $response = Invoke-WebRequest -UseBasicParsing -Method GET -MaximumRetryCount 3 -Headers $headers `
                $urlToConfigurationFile
    } catch {
        $statusCode = if ($null -ne $_.Exception.Response) {
            " HTTP status $([int]$_.Exception.Response.StatusCode)."
        } else {
            ""
        }

        throw "Failed to fetch configuration file '$urlToConfigurationFile'.$statusCode $($_.Exception.Message)"
    }

    try {
        $mergeFlowConfig = ConvertFrom-Json -InputObject $response.Content -AsHashTable -ErrorAction Stop
    } catch {
        throw "Invalid JSON in configuration file '$urlToConfigurationFile'. $($_.Exception.Message)"
    }

    if ($mergeFlowConfig -isnot [System.Collections.IDictionary] -or
        !$mergeFlowConfig.ContainsKey('merge-flow-configurations') -or
        $mergeFlowConfig['merge-flow-configurations'] -isnot [System.Collections.IDictionary]) {
        throw "Configuration file '$urlToConfigurationFile' must contain a 'merge-flow-configurations' object."
    }

    if ($mergeFlowConfig['merge-flow-configurations'].ContainsKey($MergeFromBranch)) {
        $config = $mergeFlowConfig['merge-flow-configurations'][$MergeFromBranch]
        if ($config -isnot [System.Collections.IDictionary]) {
            throw "Configuration for branch '$MergeFromBranch' in '$urlToConfigurationFile' must be an object."
        }

        Write-Host "Found Configuration"
        Write-Host $config
        return $config
    }

    Write-Host "There was no configuration found for $MergeFromBranch"
    return $null
}

# fetch configuration
$configuration = GetConfiguration

if ($configuration -ne $null) {
    $configuredMergeToBranch = $configuration['MergeToBranch']
    if ($configuredMergeToBranch -is [string] -and
        ![string]::IsNullOrWhiteSpace($configuredMergeToBranch)) {
        $MergeToBranch = $configuredMergeToBranch
    } else {
        throw "Configuration for branch '$MergeFromBranch' must contain a non-empty string 'MergeToBranch' value."
    }

    $ExtraSwitches = "";
    if ($configuration.ContainsKey('ExtraSwitches')) {
        $ExtraSwitches = $configuration['ExtraSwitches']
    }

    $ResetToTargetPaths = "";
    if ($configuration.ContainsKey('ResetToTargetPaths')) {
        # Convert array to semicolon-separated string for output
        $ResetToTargetPaths = $configuration['ResetToTargetPaths'] -join ";"
    }

    "mergeSwitchArguments=$ExtraSwitches" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "mergeToBranch=$MergeToBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "resetToTargetPaths=$ResetToTargetPaths" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "configurationFound=$true" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}
