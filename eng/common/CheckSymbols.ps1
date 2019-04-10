param(
	[Parameter(Mandatory=$true)][string]$InputPath,             # Path to directory where NuGet packages to be checked are stored
	[Parameter(Mandatory=$true)][string]$ExtractPath,           # Path to where the packages will be extracted during validation
	[Parameter(Mandatory=$true)][string]$SymbolToolPath         # Path to where dotnet symbol-tool was installed
)

function ConfirmSymbolsAvailable {
	param( 
		[string]$PackagePath,               # Path to a NuGet package
		[string]$OutputPath,                # Path to where symbols should be stored
		[string]$SymbolToolDirectory        # Path to where symbol-tool was installed
	)

	# Ensure input file exist
	if (!(Test-Path $PackagePath))
	{
		Write-Error "Input file not exist."
	}
	
	# Extensions for which we'll look for symbols
	$RelevantExtensions = @(".dll", ".exe", ".so", ".dylib")

	# How many files are missing symbol information
	$MissingSymbols = 0

	$PackageId = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
	$ExtractPath = $OutputPath+$PackageId;
	$SymbolsPath = $OutputPath+$PackageId+".Symbols";
	
    [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $ExtractPath)

	# Makes easier to reference `symbol tool`
	Push-Location $SymbolToolDirectory
	Write-Host "Changing directory to $SymbolToolDirectory"

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
			$SymbolPath = $SymbolsPath+"\"+$FileName

			# PDB file for the module
			$PdbPath = $SymbolPath.Replace($Extension, ".pdb")

			# PDB file for x-generated module
			$NGenPdb = $SymbolPath.Replace($Extension, ".ni.pdb")

			# DBG file for a .so library
			$SODbg = $SymbolPath.Replace($Extension, ".so.dbg")

			# DWARF file for a .dylib
			$DylibDwarf = $SymbolPath.Replace($Extension, ".dylib.dwarf")

			Write-Host -NoNewLine "`t Checking file $FullPath ... "
			
			.\dotnet-symbol.exe --symbols --modules $FullPath -o $SymbolsPath -d *>$null
			
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
	param( 
		# Path to a NuGet packages directory
		[string]$PackagesPath, 
		# Path to where packages will be extracted
		[string]$OutputsPath,
		# Path to symbol tool install dir
		[string]$SymbolToolDir
	)

	if (Test-Path $OutputsPath)
	{
		Remove-Item -recurse $OutputsPath
	}

	Get-ChildItem "$PackagesPath\*.nupkg" |
		ForEach-Object {
			$FileName = $_.Name
			Write-Host "Validating $FileName "
			$Status = ConfirmSymbolsAvailable "$PackagesPath\$FileName" $OutputsPath $SymbolToolDir
			
			if ($Status -ne 0)
			{
				Write-Error "Missing symbols for $Status modules in the package $FileName"
			}
		}
}

CheckSymbols $InputPath $ExtractPath $SymbolToolPath
