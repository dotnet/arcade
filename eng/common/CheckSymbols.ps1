param(
  [Parameter(Mandatory=$true)][string] $InputPath,       # Full path to directory where NuGet packages to be checked are stored
  [Parameter(Mandatory=$true)][string] $ExtractPath,     # Full path to directory where the packages will be extracted during validation
  [Parameter(Mandatory=$true)][string] $SymbolToolPath   # Full path to directory where dotnet symbol-tool was installed
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

function ConfirmSymbolsAvailable {
  param( 
    [string] $PackagePath          # Path to a NuGet package
  )

  # Ensure input file exist
  if (!(Test-Path $PackagePath))
  {
    throw "Input file does not exist: $PackagePath"
  }
  
  # Extensions for which we'll look for symbols
  $RelevantExtensions = @(".dll", ".exe", ".so", ".dylib")

  # How many files are missing symbol information
  $MissingSymbols = 0

  $PackageId = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
  $ExtractPath = $ExtractPath + $PackageId;
  $SymbolsPath = $ExtractPath + $PackageId + ".Symbols";
  
  [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $ExtractPath)

  # Makes easier to reference `symbol tool`
  Push-Location $SymbolToolPath

  Get-ChildItem -Recurse $ExtractPath |
    Where-Object {$RelevantExtensions -contains $_.Extension} |
    ForEach-Object { 
      $FullPath = $_.FullName
      $FileName = [System.IO.Path]::GetFileName($FullPath)
      $Extension = $_.Extension

      # Those below are potential symbol files that the `dotnet symbol` might
      # return. Which one will be returned depend on the type of file we are
      # checking and which type of file was uploaded.

      # The file itself is returned
      $SymbolPath = $SymbolsPath + "\" + $FileName

      # PDB file for the module
      $PdbPath = $SymbolPath.Replace($Extension, ".pdb")

      # PDB file for R2R module (created by crossgen)
      $NGenPdb = $SymbolPath.Replace($Extension, ".ni.pdb")

      # DBG file for a .so library
      $SODbg = $SymbolPath.Replace($Extension, ".so.dbg")

      # DWARF file for a .dylib
      $DylibDwarf = $SymbolPath.Replace($Extension, ".dylib.dwarf")

      Write-Host -NoNewLine "`t Checking file $FullPath ... "
  
      .\dotnet-symbol.exe --symbols --modules --microsoft-symbol-server --internal-server $FullPath -o $SymbolsPath -d *>$null
  
      if (Test-Path $PdbPath)
      {
        $SymbolType = "PDB"
      }
      elseif (Test-Path $NGenPdb)
      {
        $SymbolType = "NGen PDB"
      }
      elseif (Test-Path $SODbg)
      {
        $SymbolType = "DBG for SO"
      }  
      elseif (Test-Path $DylibDwarf)
      {
        $SymbolType = "Dward for Dylib"
      }  
      elseif (Test-Path $SymbolPath)
      {
        $SymbolType = "Module"
      }
      else
      {
        $MissingSymbols++
        Write-Host "No symbols found!"
        return
      }
  
      Write-Host "Symbols [$SymbolType] found."
    }
  
  Pop-Location

  return $MissingSymbols
}

function CheckSymbols {
  if (Test-Path $ExtractPath)
  {
    Remove-Item -recurse $ExtractPath
  }

  Get-ChildItem "$InputPath\*.nupkg" |
    ForEach-Object {
      $FileName = $_.Name
      Write-Host "Validating $FileName "
      $Status = ConfirmSymbolsAvailable "$InputPath\$FileName"
  
      if ($Status -ne 0)
      {
        Write-Error "Missing symbols for $Status modules in the package $FileName"
      }
    }
}

CheckSymbols
