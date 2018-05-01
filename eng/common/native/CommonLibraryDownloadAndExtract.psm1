# Import common library modules
Import-Module -Name (Join-Path $PSScriptRoot "CommonLibraryDownloadWithRetry.psm1")
Import-Module -Name (Join-Path $PSScriptRoot "CommonLibraryUnzip.psm1")

<#
.SYNOPSIS
Helper module to install an xcopy deployable native tool

.DESCRIPTION
Helper module to install an xcopy deployable native tool

.PARAMETER Url
Url of artifact to download

.PARAMETER InstallDirectory
Directory to extract artifact contents to

.PARAMETER Force
Force download / extraction if file or contents already exist

.PARAMETER DownloadRetries
Total number of retry attempts

.PARAMETER RetryWaitTimeInSeconds
Wait time between retry attempts in seconds

.NOTES
Returns False if download or extraction fail, True otherwise
#>
function CommonLibraryDownloadAndExtract {
    [CmdletBinding(PositionalBinding=$false)]
    Param (
        [Parameter(Mandatory=$True)]
        [string] $Url,
        [Parameter(Mandatory=$True)]
        [string] $InstallDirectory,
        [switch] $Force = $False,
        [int] $DownloadRetries = 5,
        [int] $RetryWaitTimeInSeconds = 30
    )
    # If Verbose switch is undefined, explicitly set it to false
    if (-Not (Get-Variable 'Verbose' -ErrorAction 'SilentlyContinue')) {
        $Verbose = $False
    }
    
    $TempDir = CommonLibraryGetTempPath
    $ToolFilename = Split-Path $Url -leaf
    $TempToolPath = Join-Path $TempDir $ToolFilename

    # Download native tool
    $DownloadStatus = CommonLibraryDownloadWithRetry -Url $Url `
                                                        -Path $TempToolPath `
                                                        -DownloadRetries $DownloadRetries `
                                                        -RetryWaitTimeInSeconds $RetryWaitTimeInSeconds `
                                                        -Force:$Force `
                                                        -Verbose:$Verbose

    if ($DownloadStatus -Eq $False) {
        Write-Error "Download failed"
        return $False
    }

    # Extract native tool
    $UnzipStatus = CommonLibraryUnzip -ZipPath $TempToolPath `
                                        -OutputDirectory $InstallDirectory `
                                        -Force:$Force `
                                        -Verbose:$Verbose
    
    if ($UnzipStatus -Eq $False) {
        Write-Error "Unzip failed"
        return $False
    }
    return $True
}
export-modulemember -function CommonLibraryDownloadAndExtract