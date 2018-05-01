<#
.SYNOPSIS
Download a file, retry on failure

.DESCRIPTION
Download specified file and retry if attempt fails

.PARAMETER Url
Url of file to download

.PARAMETER Path
Path to download url file to

.PARAMETER DownloadRetries
Total number of retry attempts

.PARAMETER RetryWaitTimeInSeconds
Wait time between retry attempts in seconds

.PARAMETER Force
Overwrite existing file if present
#>
function CommonLibraryDownloadWithRetry {
    [CmdletBinding(PositionalBinding=$false)]
    Param (
        [Parameter(Mandatory=$True)]
        [string] $Url,
        [Parameter(Mandatory=$True)]
        [string] $Path,
        [int] $DownloadRetries = 5,
        [int] $RetryWaitTimeInSeconds = 30,
        [switch] $Force = $False
    )
    $Attempt = 0
    $Success = $False

    if ($Force) {
        if (Test-Path $Path) {
            Remove-Item $Path -Force
        }
    }
    if (Test-Path $Path) {
        Write-Host "File '$Path' already exists, skipping download"
        return $True
    }

    $DownloadDirectory = Split-Path -ErrorAction Ignore -Path "$Path" -Parent
    if (-Not (Test-Path $DownloadDirectory)) {
        New-Item -path $DownloadDirectory -force -itemType "Directory" | Out-Null
    }

    Write-Verbose "Downloading $Url"
    while((-Not $Success) -And ($Attempt -Lt $DownloadRetries) )
    {
        try {
            Invoke-WebRequest -Uri $Url -OutFile $Path
            Write-Verbose "Downloaded to '$Path'"
            $Success = $True
        }
        catch {
            Write-Host $_
            Write-Host $_.Exception
                    
            $Attempt++
            if ($Attempt -Lt $DownloadRetries)
            {
                $AttemptsLeft = $DownloadRetries - $Attempt
                Write-Host "Download failed, $AttemptsLeft attempts remaining, will retry in $RetryWaitTimeInSeconds seconds"
                Start-Sleep -Seconds $RetryWaitTimeInSeconds
            }
        }
    }

   return $Success
}
export-modulemember -function CommonLibraryDownloadWithRetry