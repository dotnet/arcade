param(
  [Parameter(Mandatory=$true)][string] $InputPath,           # Full path to directory where Symbols.NuGet packages to be checked are stored
  [Parameter(Mandatory=$true)][string] $ExtractPath,         # Full path to directory where the packages will be extracted during validation
  [Parameter(Mandatory=$true)][string] $SourceLinkToolPath,  # Full path to directory where dotnet SourceLink CLI was installed
  [Parameter(Mandatory=$true)][string] $GHRepoName,          # GitHub name of the repo including the Org. E.g., dotnet/arcade
  [Parameter(Mandatory=$true)][string] $GHCommit             # GitHub commit SHA used to build the packages
)

# Cache/HashMap (File -> Exist flag) used to consult whether a file exist 
# in the repository at a specific commit point. This is populated by inserting
# all files present in the repo at a specific commit point.
$global:RepoFiles = @{}

$ValidatePackage = {
  param( 
    [string] $PackagePath		                             # Full path to a Symbols.NuGet package
  )

  # Ensure input file exist
  if (!(Test-Path $PackagePath)) {
    throw "Input file does not exist: $PackagePath"
  }

  # Extensions for which we'll look for SourceLink information
  # For now we'll only care about Portable & Embedded PDBs
  $RelevantExtensions = @(".dll", ".exe", ".pdb")
 
  Write-Host -NoNewLine "Validating $PackagePath ... "

  $PackageId = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
  $ExtractPath = Join-Path -Path $using:ExtractPath -ChildPath $PackageId
  $FailedFiles = 0

  Add-Type -AssemblyName System.IO.Compression.FileSystem
  [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $ExtractPath)

  Get-ChildItem -Recurse $ExtractPath |
    Where-Object { $RelevantExtensions -contains $_.Extension } |
    ForEach-Object {
      # We ignore resource DLLs
      if ($_.FullName.EndsWith(".resources.dll")) {
        return
      }
      
      $ValidateFile = {
        param( 
          [string] $FullPath,                                # Full path to the module that has to be checked
          [ref] $FailedFiles
        )

        # Makes easier to reference `sourcelink cli`
        Push-Location $using:SourceLinkToolPath

        $SourceLinkInfos = .\sourcelink.exe print-urls $FullPath | Out-String

        if ($LASTEXITCODE -eq 0 -and -not ([string]::IsNullOrEmpty($SourceLinkInfos))) {
          $NumFailedLinks = 0

          # We only care about Http addresses
          $Matches = (Select-String '(http[s]?)(:\/\/)([^\s,]+)' -Input $SourceLinkInfos -AllMatches).Matches

          if ($Matches.Count -ne 0) {
	        $Matches.Value |
	          ForEach-Object {
	            $Link = $_
                $CommitUrl = -Join("https://raw.githubusercontent.com/", $using:GHRepoName, "/", $using:GHCommit, "/")
                $FilePath = $Link.Replace($CommitUrl, "")
                $Status = 200
                $Cache = $using:RepoFiles

                if ( !($Cache.ContainsKey($FilePath)) ) {
                  try {
	                $Uri = $Link -as [System.URI]
	                
                    # Only GitHub links are valid
                    if ($Uri.AbsoluteURI -ne $null -and $Uri.Host -match "github") {
                      $Status = (Invoke-WebRequest -Uri $Link -UseBasicParsing -Method HEAD -TimeoutSec 5).StatusCode
                    }
                    else {
                      $Status = 0
                    }
                  }
                  Catch {
                    $Status = 0                
                  }
                }

		        if ($Status -ne 200) {
                  if ($NumFailedLinks -eq 0) {
                    if ($FailedFiles.Value -eq 0) {
                      Write-Host
                    }

                    Write-Host "`tFile $FullPath has broken links:"
                  }

                  Write-Host "`t`tFailed to retrieve $Link"

                  $NumFailedLinks++
		        }
	          }          
          }

          if ($NumFailedLinks -ne 0) {
            $FailedFiles.value++
	        $global:LASTEXITCODE = 1
          }
        }

        Pop-Location
      }
      
      &$ValidateFile $_.FullName ([ref]$FailedFiles)
    }

  if ($FailedFiles -eq 0) {
    Write-Host "Passed."
  }
}

function ValidateSourceLinkLinks {
  if (!($GHRepoName -Match "^[^\s\/]+/[^\s\/]+$")) {
    Write-Host "GHRepoName should be in the format <org>/<repo>"
    $global:LASTEXITCODE = 1
    return
  }

  if (!($GHCommit -Match "^[0-9a-fA-F]{40}$")) {
    Write-Host "GHCommit should be a 40 chars hexadecimal string"
    $global:LASTEXITCODE = 1
    return
  }

  $RepoTreeURL = -Join("https://api.github.com/repos/", $GHRepoName, "/git/trees/", $GHCommit, "?recursive=1")

  try {
    # Retrieve the list of files in the repo at that particular commit point and store them in the RepoFiles hash
    $Data = Invoke-WebRequest $RepoTreeURL | ConvertFrom-Json | Select -ExpandProperty tree
  
    foreach ($data in $Data) {
      $RepoFiles[$data.path] = 1
    }  
  }
  Catch {
    Write-Host "Problems downloading the list of files from the repo. Url used: $RepoTreeURL"
    $global:LASTEXITCODE = 1
    return
  }
  
  if (Test-Path $ExtractPath) {
    Remove-Item $ExtractPath -Force -Recurse -ErrorAction SilentlyContinue
  }

  # Process each NuGet package in parallel
  $Jobs = @()
  Get-ChildItem "$InputPath\*.symbols.nupkg" |
    ForEach-Object {
      $Jobs += Start-Job -ScriptBlock $ValidatePackage -ArgumentList $_.FullName
    }

  foreach ($Job in $Jobs) {
    Wait-Job -Id $Job.Id | Receive-Job
  }
}

Measure-Command { ValidateSourceLinkLinks }
