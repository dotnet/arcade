$ErrorActionPreference = 'Stop'

. $PSScriptRoot\..\github-app-functions.ps1

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]
        $Expected,

        [Parameter(Mandatory = $true)]
        $Actual,

        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Assert-ThrowsLike {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock] $Action,

        [Parameter(Mandatory = $true)]
        [string] $Pattern
    )

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -like $Pattern) {
            return
        }

        throw
    }

    throw "Expected an exception matching '$Pattern'."
}

function New-TestInstallation([long] $Id, [string] $Owner) {
    return [pscustomobject]@{
        id = $Id
        account = [pscustomobject]@{
            login = $Owner
        }
    }
}

$expectedInstallations = @(
    (New-TestInstallation 152744544 'microsoft')
    (New-TestInstallation 149883392 'dotnet')
)
$installations = @(Get-GitHubAppInstallations -GetPage {
    param($Page)
    Assert-Equal 1 $Page 'The single-page request used the wrong page.'
    return $expectedInstallations
})

Assert-Equal 2 $installations.Count 'The installation response was not flattened.'
foreach ($expected in $expectedInstallations) {
    $selected = Select-GitHubAppInstallation `
        -Installations $installations `
        -InstallationOwner $expected.account.login
    Assert-Equal $expected.id $selected.id "The wrong installation was selected for '$($expected.account.login)'."
}

$firstPage = @(0..99 | ForEach-Object { New-TestInstallation (1000 + $_) "owner-$_" })
$secondPageInstallation = New-TestInstallation 2000 'second-page-owner'
$requestedPages = [System.Collections.Generic.List[int]]::new()
$pagedInstallations = @(Get-GitHubAppInstallations -GetPage {
    param($Page)
    $requestedPages.Add($Page)
    if ($Page -eq 1) {
        return $firstPage
    }
    if ($Page -eq 2) {
        return $secondPageInstallation
    }
    throw "Unexpected page $Page."
})
$selected = Select-GitHubAppInstallation `
    -Installations $pagedInstallations `
    -InstallationOwner $secondPageInstallation.account.login

Assert-Equal 101 $pagedInstallations.Count 'Pagination did not retain every installation.'
Assert-Equal '1,2' ($requestedPages -join ',') 'Pagination requested the wrong pages.'
Assert-Equal $secondPageInstallation.id $selected.id 'The second-page installation was not selected.'

Assert-ThrowsLike {
    Select-GitHubAppInstallation -Installations $installations -InstallationOwner 'missing-owner'
} "No installation found for 'missing-owner'.*"

Assert-ThrowsLike {
    Select-GitHubAppInstallation -Installations @() -InstallationOwner 'missing-owner'
} "No installation found for 'missing-owner'.*"

$duplicateInstallations = @(
    (New-TestInstallation 1 'duplicate-owner')
    (New-TestInstallation 2 'duplicate-owner')
)
Assert-ThrowsLike {
    Select-GitHubAppInstallation -Installations $duplicateInstallations -InstallationOwner 'duplicate-owner'
} "Found multiple installations for 'duplicate-owner': 1, 2"

Write-Host 'GitHub App installation tests passed.'
