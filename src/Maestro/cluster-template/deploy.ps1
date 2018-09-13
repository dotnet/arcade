<#
 .SYNOPSIS
    Deploys a service fabric cluster
#>

param(
 [Parameter(Mandatory=$True)]
 [string]
 $subscriptionName,

 [Parameter(Mandatory=$True)]
 [string]
 $location,

 [Parameter(Mandatory=$True)]
 [string]
 $clusterName
)

Function RegisterRP
{
    Param(
        [string]$ResourceProviderNamespace
    )

    Write-Host "Registering resource provider '$ResourceProviderNamespace'";
    Register-AzureRmResourceProvider -ProviderNamespace $ResourceProviderNamespace | Out-Null;
}

function Get-ParameterFromKeyVault($parameter)
{
    Get-AzureKeyVaultSecret -VaultName $parameter.vault -Name $parameter.secret | ForEach-Object SecretValueText | ForEach-Object { ConvertTo-SecureString $_ -AsPlainText -Force }
}

function Read-Parameters($parametersFile)
{
    $unProcessedParameters = Get-Content $parametersFile | ConvertFrom-Json
    $vaultName = $unProcessedParameters.certificateSourceKeyVaultName
    $parameters = @{}
    $parameters.Add("clusterName", [string]$unProcessedParameters.clusterName)
    $parameters.Add("adminUserName", [string]$unProcessedParameters.adminUserName)
    $sslEndpoints = @()
    foreach ($endpoint in $unProcessedParameters.sslEndpoints)
    {
        $ep = @{}
        $ep.Add("internalPort", $endpoint.internalPort)
        $sslEndpoints += $ep
    }
    $parameters.Add("sslEndpoints", $sslEndpoints)
    $parameters.Add("adminPassword", (Get-ParameterFromKeyVault $unProcessedParameters.adminPassword))
    $parameters.Add("secretSourceVaultResourceId", [string](Get-AzureRmKeyVault -VaultName $vaultName | ForEach-Object ResourceId))
    $certUrls = @()
    foreach ($url in ($unProcessedParameters.certificates | ForEach-Object { Get-AzureKeyVaultCertificate -VaultName $vaultName -Name $_ } | ForEach-Object SecretId))
    {
        $certUrls += [string]$url
    }
    $parameters.Add("certificateUrls", $certUrls)
    $parameters.Add("adminClientCertificateThumbprint", [string]$unProcessedParameters.adminClientCertificateThumbprint)
    $parameters | ConvertTo-Json | Out-Host
    $parameters
}

$ErrorActionPreference = "Stop"
Set-StrictMode -Version 2.0

$artifacts = Join-Path $PSScriptRoot artifacts
$resourceGroupName = "$clusterName-cluster"


Write-Host "Selecting subscription '$subscriptionName'";
Select-AzureRmSubscription -SubscriptionName $subscriptionName;

$resourceProviders = @("microsoft.storage","microsoft.network","microsoft.compute","microsoft.servicefabric");
if($resourceProviders.length) {
    Write-Host "Registering resource providers"
    foreach($resourceProvider in $resourceProviders) {
        RegisterRP($resourceProvider);
    }
}

$OptionalParameters = New-Object -TypeName Hashtable

$ArtifactsLocationName = '_artifactsLocation'
$ArtifactsLocationSasTokenName = '_artifactsLocationSasToken'
$ArtifactsLocationStorageAccountName = '_artifactsLocationStorageAccountName'
$ArtifactsLocationStorageAccountKey = '_artifactsLocationStorageAccountKey'

$StorageAccountName = 'stage' + ((Get-AzureRmContext).Subscription.Id).Replace('-', '').substring(0, 19)
$StorageContainerName = $clusterName.ToLowerInvariant() + "-stageartifacts"
$StorageAccount = (Get-AzureRmStorageAccount | Where-Object{$_.StorageAccountName -eq $StorageAccountName})

# Create the storage account if it doesn't already exist
if (!$StorageAccount) {
    $StorageResourceGroupName = 'ARM_Deploy_Staging'
    New-AzureRmResourceGroup -Location "$location" -Name $StorageResourceGroupName -Force
    $StorageAccount = New-AzureRmStorageAccount -StorageAccountName $StorageAccountName -Type 'Standard_LRS' -ResourceGroupName $StorageResourceGroupName -Location "$location"
}

# Generate the value for artifacts location if it is not provided in the parameter file
$OptionalParameters[$ArtifactsLocationName] = $StorageAccount.Context.BlobEndPoint + $StorageContainerName

# Copy files from the local storage staging location to the storage account container
New-AzureStorageContainer -Name $StorageContainerName -Context $StorageAccount.Context -ErrorAction SilentlyContinue *>&1

$ArtifactFilePaths = Get-ChildItem $artifacts -Recurse -File | ForEach-Object -Process {$_.FullName}
foreach ($SourcePath in $ArtifactFilePaths) {
   Set-AzureStorageBlobContent -File $SourcePath -Blob $SourcePath.Substring($artifacts.length + 1) -Container $StorageContainerName -Context $StorageAccount.Context -Force
}

$OptionalParameters[$ArtifactsLocationStorageAccountName] = $StorageAccountName
$OptionalParameters[$ArtifactsLocationStorageAccountKey] = ($StorageAccount | Get-AzureRmStorageAccountKey)[0].Value
$OptionalParameters[$ArtifactsLocationSasTokenName] = (New-AzureStorageContainerSASToken -Container $StorageContainerName -Context $StorageAccount.Context -Permission r -ExpiryTime (Get-Date).AddYears(1))

$OptionalParameters[$ArtifactsLocationStorageAccountKey] = ConvertTo-SecureString $OptionalParameters[$ArtifactsLocationStorageAccountKey] -AsPlainText -Force
$OptionalParameters[$ArtifactsLocationSasTokenName] = ConvertTo-SecureString $OptionalParameters[$ArtifactsLocationSasTokenName] -AsPlainText -Force


#Create or check for existing resource group
$resourceGroup = Get-AzureRmResourceGroup -Name $resourceGroupName -ErrorAction SilentlyContinue
if(!$resourceGroup)
{
    Write-Host "Creating resource group '$resourceGroupName' in location '$location'";
    New-AzureRmResourceGroup -Name $resourceGroupName -Location $location
}
else{
    Write-Host "Using existing resource group '$resourceGroupName'";
}

$templateFilePath = Join-Path $PSScriptRoot template.json
$parametersFilePath = Join-Path $PSScriptRoot "parameters/$clusterName.json"

$parameters = Read-Parameters $parametersFilePath

# Start the deployment
Write-Host "Starting deployment...";
$OptionalParameters | ConvertTo-Json | Out-Host
New-AzureRmResourceGroupDeployment -ResourceGroupName $resourceGroupName -TemplateFile $templateFilePath -TemplateParameterObject $parameters @OptionalParameters;
