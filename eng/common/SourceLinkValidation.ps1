param(
  [Parameter(Mandatory=$true)][string] $InputPath,           # Full path to directory where Symbols.NuGet packages to be checked are stored
  [Parameter(Mandatory=$true)][string] $ExtractPath,         # Full path to directory where the packages will be extracted during validation
  [Parameter(Mandatory=$true)][string] $SourceLinkToolPath   # Full path to directory where dotnet SourceLink CLI was installed
)

Add-Type -AssemblyName System.IO.Compression.FileSystem

$script:SourceLinkInfoCache = @{}

function UrlStatusCode {
  param(
    [string] $Url		                                     # URL to be checked
  )

  if ($SourceLinkInfoCache.ContainsKey($Url)) {
	return $SourceLinkInfoCache[$Url]
  }
  
  try {
    $Status = (Invoke-WebRequest -Uri $Url -UseBasicParsing -Method HEAD -TimeoutSec 10).StatusCode
  }
  catch [Net.WebException] {
    $Status = [int]$_.Exception.Response.StatusCode
  }
  
  $SourceLinkInfoCache.Add($Url, $Status)
  
  return $Status
}

function ExtractAndTestSourceLinkLinks {
  param( 
    [string] $FullPath                                       # Full path to the module that has to be checked
  )

  $FailedLinks = 0
  $SourceLinkInfos = .\sourcelink.exe print-urls $FullPath

  if ($LASTEXITCODE -eq 0 -and -not ([string]::IsNullOrEmpty($SourceLinkInfos))) {
	((Select-String '(http[s]?)(:\/\/)([^\s,]+)' -Input $SourceLinkInfos -AllMatches).Matches.Value) |
	  ForEach-Object {
	    $Link = $_

		Write-Host -NoNewLine "| `t | `t | Checking link ($Link) ... "

		$Status = UrlStatusCode $Link
        $StatusMessage = "Passed."

		if ($Status -ne "200") {
          $StatusMessage = "Inaccessible."
		  $FailedLinks++
		}

        Write-Host "$StatusMessage. Return status was $Status"
	  }
  }
  else {
  	Write-Host "| `t | `t No SourceLink information found."
  }

  return $FailedLinks
}

function CheckSourceLinkLinks {
  param( 
    [string] $PackagePath		                             # Path to a Symbols.NuGet package
  )

  # Ensure input file exist
  if (!(Test-Path $PackagePath)) {
    throw "Input file does not exist: $PackagePath"
  }

  # Extensions for which we'll look for SourceLink information
  # For now we'll only care about Portable & Embedded PDBs
  $RelevantExtensions = @(".dll", ".exe", ".pdb")

  # How many links were inaccessible
  $FailedFiles = 0
  $PassedFiles = 0

  $PackageId = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
  $ExtractPath = Join-Path -Path $ExtractPath -ChildPath $PackageId

  [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $ExtractPath)

  # Makes easier to reference `sourcelink cli`
  Push-Location $SourceLinkToolPath

  Get-ChildItem -Recurse $ExtractPath |
    Where-Object {$RelevantExtensions -contains $_.Extension} |
    ForEach-Object {
      Write-Host "| `t Checking file" $_.FullName
	  
	  $Status = ExtractAndTestSourceLinkLinks $_.FullName

	  if ($Status -ne 0) {
	    $FailedFiles++
	  }
	  else {
	    $PassedFiles++
	  }
    }
  
  Pop-Location

  if ($PassedFiles -eq 0 -and $FailedFiles -eq 0) {
	Write-Host "| `t No files to check in this package."
  }

  return $FailedFiles
}

function CheckSourceLinkInformation {
  if (Test-Path $ExtractPath) {
    Remove-Item $ExtractPath -Force -Recurse -ErrorAction SilentlyContinue
  }

  $HasErrors = 0

  Get-ChildItem "$InputPath\*.symbols.nupkg" |
    ForEach-Object {
      $FileName = $_.Name
      Write-Host "Validating $InputPath\$FileName "
      $Status = CheckSourceLinkLinks $_.FullName
  
      if ($Status -ne 0) {
        Write-Error "| Result: Some links in $FileName were inaccessible."
		$HasErrors = 1
      }
	  else {
	    Write-Host "| Result: Passed."
	  }

	  Write-Host
    }

	$global:LASTEXITCODE = $HasErrors
}

CheckSourceLinkInformation
