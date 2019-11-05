<#
.SYNOPSIS
Copies all packages from one feed to another in azure devops

.PARAMETER SourceFeedUri
Source feed to copy from

.PARAMETER TargetFeedUri
Target feed to copy to

.PARAMETER NugetPath
Path to nuget.exe to use for pushes

.PARAMETER AzDOPat
PAT used to make VSTS modifications.  If UseKeyVault is passed, may be omitted

.PARAMETER UseKeyVault
If passed, looks up secrets in keyvault

#>

param (
    [Parameter(Mandatory=$true)]
    [string]$SourceFeedUri,
    [Parameter(Mandatory=$true)]
    [string]$TargetFeedUri,
    [Parameter(Mandatory=$true)]
    [string]$NugetPath,
    [string]$AzDOPat,
    [switch]$UseKeyVault
)

if ($UseKeyVault) {
    try {
        Write-Output "Obtaining required secrets from keyvault"
        $AzDOPat = $(Get-AzKeyVaultSecret -VaultName 'EngKeyVault' -Name 'dn-bot-dnceng-artifact-feeds-rw' -ErrorAction Stop).SecretValueText
    }
    catch {
        Write-Error $_.Exception.Message
        Write-Error "Failed to gather required credentials from EngKeyVault.  Consider passing them in directly."
        exit
    }
} else {
    if (!$AzDOPat) {
        Write-Error "If not using key vault to find secrets, please provide AzDOPat, GitHubPat and MaestroSecret"
        exit
    }
}

# Set powershell to use TLS12 so that we don't error when talking to GitHub.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$base64authinfo = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$AzDOPat"))
$vstsAuthHeader = @{"Authorization"="Basic $base64authinfo"}

if (-not $($SourceFeedUri -match "https://pkgs.dev.azure.com/(?<account>[a-zA-Z0-9]+)/(?<visibility>[a-zA-Z0-9-]+/)?_packaging/(?<feed>.+)/nuget/v3/index.json")) {
    Write-Error "$SourceFeedUri is not in the expected format."
    exit
}
$sourceFeedAccount = $Matches["account"];
$sourceFeedVisibility = $Matches["visibility"];
$sourceFeedName = $Matches["feed"];

# Get the list of packages from the source
$listOfPackages = @()
try {
    $packageListUri = "https://feeds.dev.azure.com/$sourceFeedAccount/${sourceFeedVisibility}_apis/packaging/Feeds/$sourceFeedName/packages?api-version=5.1-preview.1"
    Write-Host "Looking up packages on feed at: $packageListUri"
    $result = Invoke-WebRequest -Headers $vstsAuthHeader $packageListUri
    $resultJson = $result | ConvertFrom-Json
    Write-Host "Feed $SourceFeedUri has $($resultJson.count) packages"
    foreach ($package in $resultJson.value) {
        $versionsResult = Invoke-WebRequest -Headers $vstsAuthHeader $package._links.versions.href
        $versionsResultJson = $versionsResult | ConvertFrom-Json
        foreach ($version in $versionsResultJson.value) {
            $listOfPackages += @{ name = $package.name; version = $version.version }
        }
    }
} catch {
    Write-Error $_
    exit
}

Write-Host "Copying:"
foreach ($packageToCopy in $listOfPackages) {
    Write-Host "  $($packageToCopy.name) @ $($packageToCopy.version)"
}

# Download all packages
$downloadRootBase = $([System.IO.Path]::GetTempPath())
$downloadRoot = Join-Path -Path $downloadRootBase -ChildPath $([System.IO.Path]::GetRandomFileName())
New-Item -Path $downloadRoot -ItemType Directory | Out-Null

foreach ($packageToCopy in $listOfPackages) {
    try {
        $packageContentUrl = "https://pkgs.dev.azure.com/$sourceFeedAccount/${sourceFeedVisibility}_apis/packaging/feeds/$sourceFeedName/nuget/packages/$($packageToCopy.name)/versions/$($packageToCopy.version)/content";
        Write-Host "Downloading package $($packageToCopy.name) @ $($packageToCopy.version) from $packageContentUrl"
        $localPackagePath = Join-Path -Path $downloadRoot -ChildPath "$($packageToCopy.name).$($packageToCopy.version).nupkg"
        Invoke-WebRequest -Headers $vstsAuthHeader $packageContentUrl -OutFile $localPackagePath
        & $NugetPath push -Source $TargetFeedUri -ApiKey AzureDevOps $localPackagePath -SkipDuplicate
    } catch {
        Write-Error $_
        exit
    }
}
