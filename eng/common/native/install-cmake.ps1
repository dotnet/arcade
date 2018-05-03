<#
.SYNOPSIS
Install cmake native tool

.DESCRIPTION
Install cmake native tool from Azure blob storage

.PARAMETER InstallPath
Base directory to install native tool to

.PARAMETER BaseUri
Base file directory or Url from which to acquire tool archives

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
    [string] $BaseUri,
    [Parameter(Mandatory=$True)]
    [string] $Version,
    [string] $CommonLibraryDirectory = $PSScriptRoot,
    [switch] $Force = $False,
    [int] $DownloadRetries = 5,
    [int] $RetryWaitTimeInSeconds = 30
)

# Import common library modules
Import-Module -Name (Join-Path $CommonLibraryDirectory "CommonLibrary.psm1")

try {
    # Define verbose switch if undefined
    $Verbose = $VerbosePreference -Eq "Continue"
    
    $ToolName = "cmake"

    $Arch = CommonLibrary\Get-MachineArchitecture
    $ToolOs = "win64"
    if($Arch -Eq "x32") {
        $ToolOs = "win32"
    }
    $ToolNameMoniker = "$ToolName-$Version-$ToolOs-$Arch"
    $AssemblyPath = Join-Path $InstallPath "$ToolName\$Version\$ToolNameMoniker\bin\$ToolName.exe"

    # Install tool
    if ((Test-Path $AssemblyPath) -And (-Not $Force)) {
        Write-Verbose "$ToolName ($ToolVersion) already exists, skipping install (specify -Force to force install)"
    }
    else {
        $Uri = "$BaseUri/$ToolNameMoniker.zip"
        $ToolInstallDirectory = Join-Path $InstallPath "$ToolName\$ToolVersion\"
        $InstallStatus = CommonLibrary\DownloadAndExtract -Uri $Uri `
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
    $ShimPath = Join-Path $InstallPath "$ToolName.cmd"
    $GenerateShimStatus = CommonLibrary\New-ScriptShim -ShimPath $ShimPath `
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