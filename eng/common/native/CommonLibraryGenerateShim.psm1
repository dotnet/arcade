<#
.SYNOPSIS
Generate a shim for a native tool

.DESCRIPTION
Creates a wrapper script (shim) that passes arguments forward to native tool assembly

.PARAMETER ShimPath
Path to shim file

.PARAMETER AssemblyPath
Path to assembly that shim forwards to

.PARAMETER Overwrite
Parameter description

.NOTES
Returns $True if generating shim succeeds, $False otherwise
#>
function CommonLibraryGenerateShim {
    [CmdletBinding(PositionalBinding=$false)]
    Param (
        [Parameter(Mandatory=$True)]
        [string] $ShimPath,
        [Parameter(Mandatory=$True)]
        [string] $AssemblyPath,
        [switch] $Force
    )
    try {
        Write-Verbose "Generating '$ShimPath' shim"

        if ((Test-Path $ShimPath) -And ($Force -Eq $False)) {
            Write-Error "$ShimPath already exists, specify '-Force' to overwrite file"
            return $False
        }

        if (-Not (Test-Path $AssemblyPath)){
            Write-Error "Specified AssemblyPath '$AssemblyPath' does not exist"
            return $False
        }

        $ShimContents = "@echo off`n"
        $ShimContents += "$AssemblyPath %*"
        
        # Write shim file
        $ShimContents | Out-File $ShimPath -Encoding "ASCII"

        if ($? -Ne $True) {
            Write-Error "Failed to generate shim"
            return $False
        }
        return $True
    }
    catch {
        Write-Host $_
        Write-Host $_.Exception
        return $False
    }
}
export-modulemember -function CommonLibraryGenerateShim