<#
  .Synopsis
  Acquires dotnet cli and makes it available on the path
  .Notes
  This will use a globally installed dotnet cli if possible
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

try {
  $here = Split-Path -Parent $MyInvocation.MyCommand.Definition
  $repoRoot = Resolve-Path (Join-Path $here "../..")
  $globalJsonFile = Join-Path $repoRoot global.json
  $globalJson = Get-Content $globalJsonFile -Raw | ConvertFrom-Json

  $Path = Join-Path $repoRoot ".dotnet"
  $Version = $globalJson.sdk.version

  if ((Get-Command dotnet -ErrorAction SilentlyContinue) -and ($(dotnet --version) -eq $Version)) {
    Write-Verbose "Global dotnet cli version $Version found"
    exit 0
  }

  New-Item $Path -ItemType Directory | Out-Null
  $installScriptUri = "https://dot.net/v1/dotnet-install.ps1"
  $installScriptPath = Join-Path $Path dotnet-install.ps1

  if (!(Test-Path -PathType Leaf $installScriptPath)) {
    Invoke-WebRequest $installScriptUri -UseBasicParsing -OutFile $installScriptPath
  }

  Write-Verbose "Installing dotnet cli"
  & $installScriptPath -Version $Version -InstallDir $Path
  if ($LASTEXITCODE -ne 0 -or -not $?) {
    Write-Error "Failed to install dotnet cli (exit code $LASTEXITCODE)";
  }

  if ($env:Build_BuildNumber) {
    Write-Host "VSTS detected, setting up Dotnet Cli for use"
    Write-Host "##vso[task.prependpath]$Path"
    Write-Host "##vso[task.setvariable variable=DOTNET_SKIP_FIRST_TIME_EXPERIENCE;]1"
    Write-Host "##vso[task.setvariable variable=DOTNET_MULTILEVEL_LOOKUP;]0"
  }
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
  $env:DOTNET_MULTILEVEL_LOOKUP = "0"
  Write-Host "Installed Dotnet Cli"
  exit 0

}
catch {
  Write-Host $_
  Write-Host $_.Exception
  exit 1
}
