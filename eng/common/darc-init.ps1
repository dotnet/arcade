$verbosity = "m"
. $PSScriptRoot\init-tools.ps1

function InstallDarcCli {
  $darcCliPackageName = "microsoft.dotnet.darc"
  $toolList = Invoke-Expression "$buildDriver tool list -g"

  if ($toolList -like "*$darcCliPackageName*") {
    Invoke-Expression "$buildDriver tool uninstall $darcCliPackageName -g"
  }

  $toolsetVersion = $GlobalJson.'msbuild-sdks'.'Microsoft.DotNet.Arcade.Sdk'

  Write-Host "Installing Darc CLI version $toolsetVersion..."
  Write-Host "You may need to restart your command window if this is the first dotnet tool you have installed."
  Invoke-Expression "$buildDriver tool install $darcCliPackageName --version $toolsetVersion -v $verbosity -g"
}

InstallDarcCli
