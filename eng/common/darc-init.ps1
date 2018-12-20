$verbosity = "m"
. $PSScriptRoot\tools.ps1

function InstallDarcCli {
  $darcCliPackageName = "microsoft.dotnet.darc"

  $dotnetRoot = InitializeDotNetCli -install:$true
  $dotnet = "$dotnetRoot\dotnet.exe"
  $toolList = Invoke-Expression "& `"$dotnet`" tool list -g"

  if ($toolList -like "*$darcCliPackageName*") {
    Invoke-Expression "& `"$dotnet`" tool uninstall $darcCliPackageName -g"
  }

  # Preference the "Microsoft.DotNet.Darc" tool in global json.
  # If it exists, use that as the version, otherwise use the arcade sdk version.
  # TODO: This should eventually be replaced with a call to the build asset registry,
  # when anonymous access is available for get-builds. Then, we should grab the latest
  # build number from the prod channel, or the latest int build number if an additional
  # parameter (e.g. -prerelease) is provided to the script.
  if (Get-Member -InputObject $GlobalJson.tools -Name "Microsoft.DotNet.Darc") {
    $darcVersion = $GlobalJson.'tools'.'Microsoft.DotNet.Darc'
  } else {
    $darcVersion = $GlobalJson.'msbuild-sdks'.'Microsoft.DotNet.Arcade.Sdk'
  }

  Write-Host "Installing Darc CLI version $darcVersion..."
  Write-Host "You may need to restart your command window if this is the first dotnet tool you have installed."
  Invoke-Expression "& `"$dotnet`" tool install $darcCliPackageName --version $darcVersion -v $verbosity -g"
}

InstallDarcCli
