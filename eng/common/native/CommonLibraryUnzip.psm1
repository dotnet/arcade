<#
.SYNOPSIS
Unzip an archive

.DESCRIPTION
Powershell module to unzip an archive to a specified directory

.PARAMETER ZipPath (Required)
Path to archive to unzip

.PARAMETER OutputDirectory (Required)
Output directory for archive contents

.PARAMETER Force
Overwrite output directory contents if they already exist

.NOTES
- Returns True and does not perform an extraction if output directory already exists but Overwrite is not True.
- Returns True if unzip operation is successful
- Returns False if Overwrite is True and it is unable to remove contents of OutputDirectory
- Returns False if unable to extract zip archive
#>
function CommonLibraryUnzip {
    [CmdletBinding(PositionalBinding=$false)]
    Param (
        [Parameter(Mandatory=$True)]
        [string] $ZipPath,
        [Parameter(Mandatory=$True)]
        [string] $OutputDirectory,
        [switch] $Force
    )

    Write-Verbose "Extracting '$ZipPath' to '$OutputDirectory'"
    try {
        if ((Test-Path $OutputDirectory) -And (-Not $Overwrite)) {
            Write-Host "Directory '$OutputDirectory' already exists, skipping extract"
            return $True
        }
        if (Test-Path $OutputDirectory) {
            Write-Verbose "'Force' is 'True', but '$OutputDirectory' exists, removing directory"
            Remove-Item $OutputDirectory -Force -Recurse
            if ($? -Eq $False) {
                Write-Error "Unable to remove '$OutputDirectory'"
                return $False
            }
        }
        if (-Not (Test-Path $OutputDirectory)) {
            New-Item -path $OutputDirectory -Force -itemType "Directory" | Out-Null
        }

        Add-Type -assembly "system.io.compression.filesystem"
        [io.compression.zipfile]::ExtractToDirectory("$ZipPath", "$OutputDirectory")
        if ($? -Eq $False) {
            Write-Error "Unable to extract '$ZipPath'"
            return $False
        }
    }
    catch {
        Write-Host $_
        Write-Host $_.Exception

        return $False
    }
    return $True
}
export-modulemember -function CommonLibraryUnzip