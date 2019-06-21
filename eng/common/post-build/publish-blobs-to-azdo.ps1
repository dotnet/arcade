param(
  [Parameter(Mandatory=$true)][string] $FeedName,
  [Parameter(Mandatory=$true)][string] $SourceFolderCollection,    #';' separated string with paths to folder containing the blobs. The folder name looks like <extension>_<version>
                                                                   # i.e: C:\folderInAgent\Artifacts\PKG_1.2.3
  [Parameter(Mandatory=$true)][string] $PersonalAccessToken,
  [string] $Org = "dnceng",
  [string] $AzureCliVersion = "2.0.67"
)
        
. $PSScriptRoot\..\tools.ps1

function InstallAzureCLI ($azureCliVersion) {
    Write-Host "Downloading Azure CLI MSI Version $azureCliVersion from https://azurecliprod.blob.core.windows.net/msi/azure-cli-$azureCliVersion.msi..."
    Invoke-WebRequest "https://azurecliprod.blob.core.windows.net/msi/azure-cli-$azureCliVersion.msi" -OutFile 'azure-cli.msi'
    
    Write-Host "Installing Azure CLI..."

    .\azure-cli.msi /passive

    $azureCli = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object 'DisplayName' -Match "Microsoft Azure CLI" -ErrorAction Ignore

    while ($azureCli -eq $null) {
        Start-Sleep -s 10
        $azureCli = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object 'DisplayName' -Match "Microsoft Azure CLI" -ErrorAction Ignore
        Write-Host "Installation has not completed..."
    }

    Write-Host "CLI installed!"

    $azPathx86 = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin"
    $azPathx64 = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"

    if (Test-Path "$azPathx86") {
        $env:AzExe = "$azPathx86\az.cmd"
    } elseif (Test-Path "$azPathx64") {
        $env:AzExe = "$azPathx64\az.cmd"
    } else {
        Write-Host "az.exe install directory was not found..."
        ExitWithExitCode 1
    }

    & "$env:AzExe" extension add --name azure-devops
    & "$env:AzExe" devops project list --organization "https://dev.azure.com/$Org"
}

function SetPATAsEnvVar ($personalAccessToken) {
  Write-Host "Setting PAT as an environment variable..."
  Write-LoggingCommand -Area 'task' -Event 'setvariable' -Data "" -Properties @{
    'variable' = 'AZURE_DEVOPS_EXT_PAT'
    'issecret' = 'false'
  } 
}

function PublishUniversalPackages()
{
  foreach ($sourceFolder in $SourceFolderCollection.Split(";")) {
    $folders = $sourceFolder.Split("\\")
    $folderName = $folders[$folders.Count - 1]
    $packageName,$packageVersion = $folderName.Split("_")
    PublishUniversalPackage $packageName $packageVersion $sourceFolder
  }
}

function PublishUniversalPackage($packageName,$packageVersion,$sourceFolder)
{
    & "$env:AzExe" artifacts universal publish `
        --organization "https://dev.azure.com/$Org" `
        --feed "$FeedName" `
        --name "$packageName" `
        --version "$packageVersion" `
        --description "Use  amore descriptive description here" `
        --path "$sourceFolder" 
}

SetPATAsEnvVar $PersonalAccessToken
InstallAzureCLI $AzureCliVersion
PublishUniversalPackages 

