<#
.SYNOPSIS
Lists all AzDO pipelines (public and internal projects) associated with given GitHub repository.
Associated = YAML definition of the pipeline is in that repo.

.PARAMETER GitHubRepository
Repository for which you want the associated pipelines listed, e.g. dotnet/runtime

.PARAMETER PAT
AzDO personal access token from the Internal project (https://dev.azure.com/dnceng/internal).
Scopes needed are repository and build/pipeline reads.
How to get a PAT:
https://docs.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate

#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $True)]
    [string] $GitHubRepository,
    [Parameter(Mandatory = $True)]
    [string] $PAT
)

function Write-Pipeline {
    $pipeline = $args[0]
    Write-Output "  - $($pipeline.name)"
    Write-Output "    $($pipeline._links.web)"
    Write-Output ""
}

$internalApiEndpoint = "https://dev.azure.com/dnceng/internal/_apis/"
$publicApiEndpoint = "https://dev.azure.com/dnceng/public/_apis/"
$azdoRepository = $GitHubRepository.Replace('/', '-')

$B64Pat = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes(":$PAT"))

$headers = @{
    "Authorization" = "Basic $B64Pat"
}

Write-Output "Looking up the $azdoRepository repository in the internal project..."

$repository = Invoke-RestMethod -Method "GET" -Uri "${internalApiEndpoint}git/repositories/${azdoRepository}?api-version=6.0" -Headers $headers
$repositoryId = $repository.id

Write-Output "Found the repository ($($repository.Id))."
Write-Output "Looking up build definitions..."

$internalBuildDefinitions = Invoke-RestMethod -Method "GET" -Uri "${internalApiEndpoint}build/definitions?api-version=6.0&repositoryId=${repositoryId}&repositoryType=TfsGit" -Headers $headers
$publicBuildDefinitions = Invoke-RestMethod -Method "GET" -Uri "${publicApiEndpoint}build/definitions?api-version=6.0&repositoryId=${GitHubRepository}&repositoryType=GitHub"

Write-Output "[INTERNAL] Pipelines based on the internal mirrored repository ($($internalBuildDefinitions.count)):"
$internalBuildDefinitions.value | ForEach-Object {Write-Pipeline $_}

Write-Output "[PUBLIC] Pipelines based on the GitHub repository ($($publicBuildDefinitions.count)):"
$publicBuildDefinitions.value | ForEach-Object {Write-Pipeline $_}
