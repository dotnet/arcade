set-strictmode -version 2.0
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$DarcCliPackageName = "microsoft.dotnet.darc" 

function Install-Dotnet-Cli {
  $RepoRoot = Join-Path $PSScriptRoot "..\.."
  $GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot "global.json") | ConvertFrom-Json
  $DotnetRoot = Join-Path $RepoRoot ".dotnet"
  $InstallScript = "$DotnetRoot\dotnet-install.ps1"
  $Version = $GlobalJson.tools.dotnet

  if (!(Test-Path $DotnetRoot)) {
    New-Item -path $DotnetRoot -force -itemType "Directory" | Out-Null
  }
  
  Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $InstallScript

  & $InstallScript -Version $Version -InstallDir $DotnetRoot
  
  if ($lastExitCode -ne 0) {
    Write-Host "Failed to install dotnet cli (exit code '$lastExitCode')." -ForegroundColor Red
    exit $lastExitCode
  }
}

function Install-Darc-Cli {
  $DarcCli = Get-Content -Raw -Path "darc-cli-settings.json" | ConvertFrom-Json
  $DarcCliFeed = $DarcCli.feed
  $DarcCliVersion = $DarcCli.version
  $ToolList = iex "dotnet tool list -g"

  if ($ToolList -like "*$DarcCliPackageName*") {
    iex "dotnet tool uninstall $DarcCliPackageName -g"
  }

  Write-Host "Installing Darc CLI version $DarcCliVersion..."
  iex "dotnet tool install $DarcCliPackageName --version $DarcCliVersion --add-source $DarcCliFeed -g"
}


try {
  Install-Dotnet-Cli
  Install-Darc-Cli
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process
}

