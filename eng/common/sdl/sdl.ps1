
function Install-Gdn {
    param(
        [string]$Path,

        # If omitted, install the latest version of Guardian, otherwise install that specific version.
        [string]$Version
    )

    if ($Version) {
        Start-Process nuget -Verbose -ArgumentList "install", "Microsoft.Guardian.Cli", "-Version $Version", "-Source https://securitytools.pkgs.visualstudio.com/_packaging/Guardian/nuget/v3/index.json", "-OutputDirectory $Path", "-NonInteractive", "-NoCache" -NoNewWindow -Wait
    } else {
        Start-Process nuget -Verbose -ArgumentList "install", "Microsoft.Guardian.Cli", "-Source https://securitytools.pkgs.visualstudio.com/_packaging/Guardian/nuget/v3/index.json", "-OutputDirectory $Path", "-NonInteractive", "-NoCache" -NoNewWindow -Wait
    }

    $gdnCliPath = Get-ChildItem -Filter guardian.cmd -Recurse -Path $Path
    return $gdnCliPath.FullName
}
