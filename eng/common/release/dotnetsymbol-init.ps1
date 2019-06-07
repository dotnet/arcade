param (
    $dotnetsymbolVersion = $null
)

$verbosity = "m"
. $PSScriptRoot\..\tools.ps1

function Installdotnetsymbol ($dotnetsymbolVersion) {
  $dotnetsymbolPackageName = "dotnet-symbol"

  $dotnetRoot = InitializeDotNetCli -install:$true
  $dotnet = "$dotnetRoot\dotnet.exe"
  $toolList = Invoke-Expression "& `"$dotnet`" tool list -g"

  if ($toolList -like "*$dotnetsymbolPackageName*") {
    Invoke-Expression "& `"$dotnet`" tool uninstall $dotnetsymbolPackageName -g"
  }
 
  Write-Host "Installing dotnet-symbol version $dotnetsymbolVersion..."
  Write-Host "You may need to restart your command window if this is the first dotnet tool you have installed."
  Invoke-Expression "& `"$dotnet`" tool install $dotnetsymbolPackageName --version $dotnetsymbolVersion -v $verbosity -g"
}

Installdotnetsymbol $dotnetsymbolVersion
