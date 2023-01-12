<#
.SYNOPSIS
For a series of known or specified channels, lists out what would be installed for a given quality level and channel.

.PARAMETER Quality
Optional quality(s) to check

.PARAMETER Channels
Optional channel(s) to check. Otherwise checks a bunch of known channels.

.PARAMETER Components
Optional channel(s) to check. Otherwise checks a bunch of known components.

#>

param (
    [string[]]$Qualities = @("preview", "ga", "daily", "signed", "validated"),
    [string[]]$Channels = @("6.0", "6.0.1xx", "6.0.3xx", "6.0.4xx", "7.0", "7.0.1xx", "7.0.2xx", "8.0", "8.0.1xx"),
    [string[]]$Components = @("runtime", "windowsdesktop", "sdk", "aspnetcore"),
    [switch]$Json
)

$queries = New-Object System.Collections.ArrayList
foreach ($channel in $Channels) {
    foreach ($quality in $Qualities) {
        foreach ($component in $Components) {
            # Filter out ones we know will be wrong
            if ($component -ne "sdk" -and $channel -like '*xx*') {
                continue
            }
            $queries += @{channel = $channel; quality = $quality; component = $component; asJson = $Json}
        }
    }
}

if ($Json) {
    Write-Host "["
}

$queries | ForEach-Object -Parallel {
    function DoLookup($channel, $quality, $component) {
        $akaMSPrefix = "https://aka.ms/dotnet"

        $versionStringUrl = ""
        if ($quality -eq "ga") {
            $versionStringUrl = "$akaMSPrefix/$channel/$component-productVersion.txt"
        }
        else {
            $versionStringUrl = "$akaMSPrefix/$channel/$quality/$component-productVersion.txt"
        }
        
        try {
            $response = Invoke-WebRequest $versionStringUrl

            # It's difficult to tell whether the aka.ms link 404s or not, since it redirects in case of
            # a not found.
            if ($response.StatusCode -ne 200 -or
                $response.Headers.'Content-Type' -eq 'text/html') {
                return "N/A"
            }

            return [System.Text.Encoding]::ASCII.GetString($response.Content).Trim()
        }
        catch
        {
            return 'N/A'
        }
    }
    
    $channel = $_.channel
    $quality = $_.quality
    $component = $_.component
    $asJson = $_.asJson

    $elementInfo = @{channel = $channel; quality = $quality; component = $component; version = $(DoLookup $channel $quality $component)}
    if ($asJson) {
        Write-Host "$($elementInfo | ConvertTo-Json),"
    } else {
        $formatStr = "{0, -10}{1, -10}{2, -16}{3, -10}" -f $elementInfo.channel, $elementInfo.quality, $elementInfo.component, $elementInfo.version
        Write-Host $formatStr
    }
}

if ($Json) {
    Write-Host "]"
}