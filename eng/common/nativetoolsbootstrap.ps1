<#
.SYNOPSIS
Entry point script for installing native tools

.DESCRIPTION
Reads $RepoRoot\eng\NativeToolsVersion.txt file to determine native assets to install
and executes installers for those tools

.PARAMETER BaseUri
Base file directory or Url from which to acquire tool archives

.PARAMETER Clean
Switch specifying to not install anything, but cleanup native asset folders

.PARAMETER Force
Force install of tools even if they previously exist

.PARAMETER DownloadRetries
Total number of retry attempts

.PARAMETER RetryWaitTimeInSeconds
Wait time between retry attempts in seconds

.NOTES
#>
[CmdletBinding(PositionalBinding=$false)]
Param (
    [string] $BaseUri = "https://dotnetfeed.blob.core.windows.net/chcosta-test/nativeassets",
    [switch] $Clean = $False,
    [switch] $Force = $False,
    [int] $DownloadRetries = 5,
    [int] $RetryWaitTimeInSeconds = 30
)

Set-StrictMode -version 2.0
$ErrorActionPreference="Stop"

Import-Module -Name (Join-Path $PSScriptRoot "native\CommonLibrary.psm1")

try {
    # Define verbose switch if undefined
    $Verbose = $VerbosePreference -Eq "Continue"

    $RepoRoot = Convert-Path -Path (Join-Path $PSScriptRoot "..\..\")
    $EngCommonBaseDir = Join-Path $PSScriptRoot "native\"
    $ArtifactsNativeBaseDir = Join-Path $RepoRoot "artifacts\native\"
    $ArtifactsInstallBin = Join-Path $ArtifactsNativeBaseDir "bin"

    if ($Clean -Or $Force) {
        Write-Host "Cleaning '$ArtifactsNativeBaseDir'"
        if (Test-Path $ArtifactsNativeBaseDir) {
            Remove-Item $ArtifactsNativeBaseDir -Force -Recurse
        }
        $TempDir = CommonLibrary\Get-TempPath
        Write-Host "Cleaning '$TempDir'"
        if (Test-Path $TempDir) {
            Remove-Item $TempDir -Force -Recurse
        }

        if ($Clean) {
            exit 0
        }
    }

    # Process tools list
    $ToolsForInstallFile = Join-Path $RepoRoot "eng\NativeToolsVersions.txt"
    Write-Host "Processing $ToolsForInstallFile"
    If (-Not (Test-Path $ToolsForInstallFile)) {
        Write-Host "No native tool dependencies are defined in '$ToolsForInstallFile'"
        exit 0
    }
    $ToolsList = ((Get-Content $ToolsForInstallFile) -replace ',','=') -join "`n" | ConvertFrom-StringData

    Write-Verbose "Required native tools:"
    $ToolsList.GetEnumerator() | ForEach-Object {
        $Key = $_.Key
        $Value = $_.Value
        Write-Verbose "- $Key ($Value)"
    }

    # Execute installers
    Write-Host "Executing installers"
    $ToolsList.GetEnumerator() | ForEach-Object {
        $ToolName = $_.Key
        $ToolVersion = $_.Value
        $InstallerFilename = "install-$ToolName.ps1"
        $LocalInstallerCommand = Join-Path $EngCommonBaseDir $InstallerFilename
        $LocalInstallerCommand += " -InstallPath $ArtifactsInstallBin"
        $LocalInstallerCommand += " -BaseUri $BaseUri"
        $LocalInstallerCommand += " -CommonLibraryDirectory $EngCommonBaseDir"
        $LocalInstallerCommand += " -Version $ToolVersion"

        if ($Verbose) {
            $LocalInstallerCommand += " -Verbose"
        }
        if (Get-Variable 'Force' -ErrorAction 'SilentlyContinue') {
            if($Force) {
                $LocalInstallerCommand += " -Force"
            }
        }

        Write-Verbose "Installing $ToolName version $ToolVersion"
        Write-Verbose "Executing '$LocalInstallerCommand'"
        Invoke-Expression "$LocalInstallerCommand"
        if ($LASTEXITCODE -Ne "0") {
            Write-Error "Execution failed"
            exit 1
        }
    }

    if (Test-Path $ArtifactsInstallBin) {
        Write-Host "Native tools are available from" (Convert-Path -Path $ArtifactsInstallBin)
    }
    else {
        Write-Error "Native tools install directory does not exist, installation failed"
        exit $False
    }
    exit 0
}
catch {
    Write-Host $_
    Write-Host $_.Exception
    exit 1    
}
