<#
.SYNOPSIS
Performs a set of actions to enable a repo in VSTS.

.DESCRIPTION
- Makes an internal repo on dev.azure.com/dnceng for the GitHub repo
- Adds a Maestro webhook
- Adds a folder structure on internal and public VSTS for build definitions

.PARAMETER GitHubRepoName
Repository name of the GitHub repo to onboard, in org/repo form.

.PARAMETER GitHubPat
PAT used to make GitHub hook modifications.  If UseKeyVault is passed, may be omitted

.PARAMETER VSTSPat
PAT used to make VSTS modifications.  If UseKeyVault is passed, may be omitted

.PARAMETER MaestroSecret
Secret for Maestro webhooks.  If UseKeyVault is passed, may be omitted

.PARAMETER DryRun
Perform no modifications

.PARAMETER UseKeyVault
If passed, looks up secrets in keyvault

#>

param (
    # GitHub Repo to onboard
    [Parameter(Mandatory=$true)]
    [string]$GitHubRepoName,
    # GitHub PAT used to create a PR to dotnet/versions and add Maestro webhook
    [string]$GitHubPat,
    # VSTS PAT used to create VSTS primitives
    [string]$VSTSPat,
    # Secret for Maestro webhook
    [string]$MaestroSecret,
    # Do not create build definitions
    [switch]$SkipBuildDefinitions,
    # Perform no actual actions, only validation
    [switch]$DryRun,
    # Connect to keyvault to grab credentials, rather than use passed in credentials
    [switch]$UseKeyVault
)

function Create-VSTS-Folder {
    param ($folderName, $vstsInstance)
    try {
        $existsResponse = Invoke-WebRequest -Method Get -Headers $vstsAuthHeader -Uri "${vstsInstance}/_apis/build/folders/${folderName}?api-version=4.1-preview.1"
    }
    catch {
        Write-Error "Failed to query for existing folders"
        Write-Error "$_.Exception"
        exit
    }

    $existingFoldersJson = ConvertFrom-Json $existsResponse.Content
    if ($existingFoldersJson.count -ne 0) {
        Write-Output "Folder $folderName already exists in ${vstsInstance}, skipping creation"
    } else {
        if (!$DryRun) {
            try {
                Invoke-WebRequest -Method Put -ContentType "application/json" -Headers $vstsAuthHeader -Uri "${vstsInstance}/_apis/build/folders/${folderName}?api-version=4.1-preview.1" -Body "{}" | Out-Null
                Write-Output "Successfully created folder $folderName at $vstsInstance"
            } catch {
                Write-Error "Failed to create folder $folderName at $vstsInstance"
                Write-Error "$_.Exception"
                exit
            }
        }
    }
}

$vstsInternalInstance = "https://dev.azure.com/dnceng/internal"  
$vstsPublicInstance = "https://dev.azure.com/dnceng/public"

Write-Output "This script will create the basic set of primitives for onboarding a repo onto dev.azure.com/dnceng:"
Write-Output "  - An internal copy of the GitHub repo."
Write-Output "  - A webhook to ensure mirror the repo."
Write-Output "  - Folder structure for public and internal CI."
Write-Output ""

# If UseKeyVault is set, grab keys from keyvault

if ($UseKeyVault) {
    try {
        Write-Output "Obtaining required secrets from keyvault"
        $GitHubPat = $(Get-AzureKeyVaultSecret -VaultName 'EngKeyVault' -Name 'dotnet-bot-user-repo-adminrepohook-pat' -ErrorAction Stop).SecretValueText
        $VSTSPat = $(Get-AzureKeyVaultSecret -VaultName 'EngKeyVault' -Name 'dn-bot-dnceng-all-scopes' -ErrorAction Stop).SecretValueText
        $MaestroSecret = $(Get-AzureKeyVaultSecret -VaultName 'EngKeyVault' -Name 'Maestro-WebhookSecretToken' -ErrorAction Stop).SecretValueText
    }
    catch {
        Write-Error $_.Exception.Message
        Write-Error "Failed to gather required credentials from EngKeyVault.  Consider passing them in directly."
        exit
    }
} else {
    if (!$VSTSPat -or !$GitHubPat -or !$MaestroSecret) {
        Write-Error "If not using key vault to find secrets, please provide VSTSPat, GitHubPat and MaestroSecret"
        exit
    }
}

# Set powershell to use TLS12 so that we don't error when talking to GitHub.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$githubTokenSuffix = "?access_token=$GitHubPat"
$base64authinfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$VSTSPat"))
$vstsAuthHeader = @{"Authorization"="Basic $base64authinfo"}

# Grab the repo information, normal casing, and compute names
$githubRepoInfo = Invoke-WebRequest "https://api.github.com/repos/$GitHubRepoName$githubTokenSuffix"
if ($githubRepoInfo.StatusCode -ne 200) {
    Write-Error "Failed to obtain repo information for $GithubRepoName.  Please ensure the repo exists and the PAT provided can access it"
    exit
}
$githubRepoInfo = ConvertFrom-Json $githubRepoInfo

# Normalize the repo name and compute the VSTS repo name
$githubOrg = $githubRepoInfo.owner.login
$githubRepo = $githubRepoInfo.name
$vstsRepo = "$githubOrg-$githubRepo"

# Check that the repo doesn't already exist on dev.azure.com/dnceng's internal instance.  Unfortunately
# we can't tell the difference here between a bad PAT and a missing repo.
Write-Output "Determining whether VSTS mirror repo already exists"

try {
    Invoke-WebRequest -Headers $vstsAuthHeader "${vstsInternalInstance}/_apis/git/repositories/${vstsRepo}?api-version=4.1" | Out-Null
    Write-Output "$vstsRepo already exists, skipping creation"
} catch {
    Write-Output "Creating Repo $vstsRepo in $vstsInternalInstance"
    if (!$DryRun) {
        $response = Invoke-WebRequest -ContentType "application/json" -Method Post -Headers $vstsAuthHeader "${vstsInternalInstance}/_apis/git/repositories?api-version=4.1" -Body "{`"name`":`"${vstsRepo}`"}"
        if ($response.StatusCode -ne 201) {
            Write-Error "Failed to create repository $vstsRepo at $vstsInternalInstance"
            exit
        } else {
            Write-Output "Successfully created repository $vstsRepo at $vstsInternalInstance"
        }
    }
}

# Create Maestro webhook (if not already there)
Write-Output "Creating Maestro webhook for mirroring"

$githubHookInfo = Invoke-WebRequest "https://api.github.com/repos/$GitHubRepoName/hooks$githubTokenSuffix"
if ($githubHookInfo.StatusCode -ne 200) {
    Write-Error "Failed to obtain hook information for $GithubRepoName.  Please ensure the repo exists and the PAT provided can access it"
}
$githubHookInfo = ConvertFrom-Json $githubHookInfo
$maestroHookUrl = "http://maestro-prod.azurewebsites.net/CommitPushed"
$alreadyHasHook = $githubHookInfo.config.url.Contains("$maestroHookUrl")

if ($alreadyHasHook) {
    Write-Output "$GithubRepoName already has Maestro webhook, skipping"
} elseif (!$DryRun) {
    $githubHookCreate = Invoke-WebRequest -Method Post "https://api.github.com/repos/$GitHubRepoName/hooks$githubTokenSuffix" -Body "{`"name`": `"web`", `"active`": true, `"events`": [`"push`"], `"config`": { `"url`": `"$maestroHookUrl`", `"content_type`": `"json`", `"secret`":`"$maestroHookSecret`" } }"
    if ($githubHookCreate.StatusCode -ne 201) {
        Write-Error "Failed to create Maestro webhook for $GithubRepoName"
        exit
    } else {
        Write-Output "Created Maestro webhook for $GitHubRepoName"
    }
}

Write-Output "Creating folders in public and internal projects"
Create-VSTS-Folder "$githubOrg/$githubRepo" $vstsInternalInstance
Create-VSTS-Folder "$githubOrg/$githubRepo" $vstsPublicInstance
