function Get-GitHubAppInstallations {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $GetPage
    )

    $installations = [System.Collections.Generic.List[object]]::new()
    $page = 1
    do {
        $pageResponse = & $GetPage $page
        $pageInstallations = @($pageResponse | ForEach-Object { $_ })
        foreach ($installation in $pageInstallations) {
            $installations.Add($installation)
        }
        $page++
    } while ($pageInstallations.Count -eq 100)

    return $installations.ToArray()
}

function Select-GitHubAppInstallation {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [object[]] $Installations,

        [Parameter(Mandatory = $true)]
        [string] $InstallationOwner
    )

    $matchingInstallations = @($Installations | Where-Object { $_.account.login -ieq $InstallationOwner })
    if ($matchingInstallations.Count -eq 0) {
        $found = ($Installations | ForEach-Object { $_.account.login }) -join ', '
        throw "No installation found for '$InstallationOwner'. App is installed on: $found"
    }
    if ($matchingInstallations.Count -ne 1) {
        $matchingIds = ($matchingInstallations | ForEach-Object { $_.id }) -join ', '
        throw "Found multiple installations for '$InstallationOwner': $matchingIds"
    }

    return $matchingInstallations[0]
}
