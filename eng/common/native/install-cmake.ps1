<#
.SYNOPSIS
Install cmake native tool

.DESCRIPTION
Install cmake native tool from Azure blob storage

.PARAMETER InstallPath
Base directory to install native tool to

.PARAMETER AzureStorageUrl
Url to Azure blob storage containing native assets

.PARAMETER AzureContainerName
Container name in Azure blob storage containing native tool archives

.PARAMETER CommonLibraryDirectory
Path to folder containing common library modules

.PARAMETER Force
Force install of tools even if they previously exist

.PARAMETER DownloadRetries
Total number of retry attempts

.PARAMETER RetryWaitTimeInSeconds
Wait time between retry attempts in seconds

.NOTES
Returns 0 if install succeeds, 1 otherwise
#>
[CmdletBinding(PositionalBinding=$false)]
Param (
    [Parameter(Mandatory=$True)]
    [string] $InstallPath,
    [Parameter(Mandatory=$True)]
    [string] $AzureStorageUrl,
    [Parameter(Mandatory=$True)]
    [string] $AzureContainerName,
    [Parameter(Mandatory=$True)]
    [string] $Version,
    [string] $CommonLibraryDirectory = $PSScriptRoot,
    [switch] $Force = $False,
    [int] $DownloadRetries = 5,
    [int] $RetryWaitTimeInSeconds = 30
)

# Import common library modules
Import-Module -Name (Join-Path $CommonLibraryDirectory "CommonLibraryModules.psm1")

try {
    # Define verbose switch if undefined
    $Verbose = $VerbosePreference -Eq "Continue"
    
    $ToolName = "cmake"

    # defined in CommonLibraryGetArchitecture.psm1
    $Arch = CommonLibraryGetArchitecture
    $ToolOs = "win64"
    if($Arch -Eq "x32") {
        $ToolOs = "win32"
    }
    $ToolNameMoniker = "$ToolName-$Version-$ToolOs-$Arch"

    # Install tool
    if ((Test-Path $$AssemblyPath) -And (-Not $Force)) {
        Write-Verbose "$ToolName ($ToolVersion) already exists, skipping install (specify -Force to force install)"
    }
    else {
        $Url = "$AzureStorageUrl/$AzureContainerName/nativeassets/$ToolNameMoniker.zip"
        $ToolInstallDirectory = Join-Path $InstallPath "$ToolName\$ToolVersion\"
        $InstallStatus = CommonLibraryDownloadAndExtract -Url $Url `
                                                         -InstallDirectory $ToolInstallDirectory `
                                                         -Force:$Force `
                                                         -DownloadRetries $DownloadRetries `
                                                         -RetryWaitTimeInSeconds $RetryWaitTimeInSeconds `
                                                         -Verbose:$Verbose

        if ($InstallStatus -Eq $False) {
            Write-Error "Installation failed"
            exit 1
        }
    }
    # Generate shim
    # Always rewrite shims so that we are referencing the expected version
    $AssemblyPath = Join-Path $InstallPath "$ToolName\$Version\$ToolNameMoniker\bin\$ToolName.exe"
    $ShimPath = Join-Path $InstallPath "$ToolName.cmd"
    $GenerateShimStatus = CommonLibraryGenerateShim -ShimPath $ShimPath `
                                                    -AssemblyPath $AssemblyPath `
                                                    -Force `
                                                    -Verbose:$Verbose

    if ($GenerateShimStatus -Eq $False) {
        Write-Error "Generate shim failed"
        return 1
    }
    
    exit 0
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    exit 1    
}