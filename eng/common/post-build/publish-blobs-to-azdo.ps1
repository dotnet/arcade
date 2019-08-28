param(
  [Parameter(Mandatory=$true)][string] $FeedName,
  [Parameter(Mandatory=$true)][string] $ArtifactStagingDirectory,
  [Parameter(Mandatory=$true)][string] $BlobCategories,
  [string] $IsStable = 'false',
  [string] $Organization = "dnceng",
  [string] $AzureCliVersion = "2.0.67",
  [string] $UniversalPackageVersion = ""
)

. $PSScriptRoot\post-build-utils.ps1

$WarningPreference = "SilentlyContinue"

# This class is a port from 
# https://github.com/dotnet/arcade-services/blob/master/src/Maestro/Microsoft.DotNet.Maestro.Tasks/src/VersionManager.cs
# which is used to get the version from a blob name. When Azure DevOps provide
# a better way to publish nupkgs, Universal Packages, etc. outside from a YAML 
# task we'll move the logic to the Microsoft.DotNet.Build.Tasks.Feed package 
# which can then reference Microsoft.DotNet.Maestro.Tasks. Moving the YAML 
# tasks to Microsoft.DotNet.Build.Tasks.Feed is tracked by this issue:
# https://github.com/dotnet/arcade/issues/3161
class VersionManager{   
  [string[]]$KnownTags = @("alpha",
                           "beta",
                           "preview",
                           "prerelease",
                           "servicing",
                           "preview",
                           "ci",
                           "dev"
                           );    
                     
  [string]CheckIfVersionInPath($pathSegments) {
    foreach ($pathSegment in $pathSegments) {
      $version = $this.GetVersion($pathSegment)
      if ($version -ne $null -and $version -ne "") {
        return $version
      }
    }

    return $null
  }

  [bool]IsMajorAndMinor($major,$minor) {
    $min = $null 
    return ($major -Match "^[0-9]*$") -and ([int32]::TryParse($minor, [ref]$min))
  }

  [string]GetMajor($versionSegment) {
    $v = $null
    if ([int32]::TryParse($versionSegment, [ref]$v))
    {
      return $versionSegment
    }

    $index = $versionSegment.Length - 1;
    $version = New-Object System.Collections.Generic.List[char]

    while ($index -gt 0 -and [char]::IsDigit($versionSegment[$index])) {
      $version.Insert(0, $versionSegment[$index--]);
    }

    return -Join $version
  }

  [bool]IsValidSegment($versionSegment) {
    if ($versionSegment -Match "^[0-9]*$") {
      return $true
    }

    foreach ($knownTag in $this.KnownTags) {
      if ($versionSegment.Contains($KnownTag)) {
        return $true
      }
    }
    
    return $false
  }

  [string]GetVersion($assetName) {
    $pathVersion = $null
    $version = $null

    if ($assetName.Contains('/')) {
      $pathSegments = $assetName.Split("/")
      $pathVersion = $this.CheckIfVersionInPath($pathSegments)
      $assetName = $pathSegments[$pathSegments.Length - 1]
    }
  
    $segments = $assetName.Split(".");
    $sb = New-Object -TypeName "System.Text.StringBuilder"
    $versionStart = 0
    $versionEnd = 0

    for ($i = 1; $i -lt $segments.Length; $i++)
    {
      $prevSegment = $segments[$i-1]
      $currSegment = $segments[$i]
      if ($this.IsMajorAndMinor($prevSegment, $currSegment)) {
        $versionStart = $i - 1
        $versionEnd = $i
        $i++

        while ($i -lt $segments.Length) {
          $segment = $segments[$i]
          if ($this.IsValidSegment($segment)) {
            $versionEnd = $i;
          }

          $i++;
        }
      }
    }

    if ($versionStart -eq $versionEnd) {
      return $pathVersion
    }
  
    $major = $this.GetMajor($segments[$versionStart++])
    $sb.Append("$major.")
  
    while ($versionStart -lt $versionEnd) {
      $segment = $segments[$versionStart]
      $sb.Append("$segment.")
      $versionStart++
    }

    $segment = $segments[$versionEnd]
    $sb.Append("$segment");

    $version = $sb.ToString()
      
    if (($pathVersion -ne $null -and $pathVersion -ne "") -and ($version -ne $null -and $version -ne "")) {
      return $null
    }

    if ($this.ValidateVersionAndPathVersion($version, $pathVersion)) {
      if ($version.Length -lt $pathVersion.Length) {
        return $version
      }
      
      return $pathVersion
    }

    if ($version -ne "") {
      $versionSegments =  $version.Split(".")
      if (!($versionSegments[$versionSegments.Count-1] -Match "^[a-z]*$")) {
        return $version
      } 
    }

    if ($pathVersion -eq $null -or $pathVersion -eq "") {
      foreach ($knownTag in $this.KnownTags) {
        if ($version.Contains($KnownTag)) {
          return "$version"
        }
      }
    }
  
    return $pathVersion
  }

  [bool]ValidateVersionAndPathVersion($version,$pathVersion) {
    if (($pathVersion -ne $null -and $pathVersion -ne "") -and ($version -ne $null -and $version -ne "")) {
      $found = $false
      foreach ($knownTag in $this.KnownTags) {
        if ($version.Contains($KnownTag)) {
          $found = $true
          break
        }
      }

      if (!$found) {
        return $false
      }

      foreach ($knownTag in $this.KnownTags) {
        if ($pathVersion.Contains($KnownTag)) {
          return $true
         }
      }
    }

    return $false
  }
}
    
function InstallAzureCLI () {
  $azureCli = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object 'DisplayName' -Match "Microsoft Azure CLI" -ErrorAction Ignore

  if ($azureCli -ne $null) {
    $env:AzExe = 'az'
    Write-Host "Azure CLI already installed!"
  } else {
    Write-Host "Downloading Azure CLI MSI Version $AzureCliVersion from https://azurecliprod.blob.core.windows.net/msi/azure-cli-$AzureCliVersion.msi..."
    Invoke-WebRequest "https://azurecliprod.blob.core.windows.net/msi/azure-cli-$AzureCliVersion.msi" -OutFile 'azure-cli.msi'
    
    Write-Host "Installing Azure CLI..."

    .\azure-cli.msi /passive

    $azureCli = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object 'DisplayName' -Match "Microsoft Azure CLI" -ErrorAction Ignore

    while ($azureCli -eq $null) {
        Start-Sleep -s 10
        $azureCli = Get-ItemProperty HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\* | Where-Object 'DisplayName' -Match "Microsoft Azure CLI" -ErrorAction Ignore
        Write-Host "Installation has not completed..."
    }

    $azPathx86 = "C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin"
    $azPathx64 = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin"

    if (Test-Path "$azPathx86") {
        $env:AzExe = "$azPathx86\az.cmd"
    } elseif (Test-Path "$azPathx64") {
        $env:AzExe = "$azPathx64\az.cmd"
    } else {
        Write-Host "az.exe install directory was not found..."
        ExitWithExitCode 1
    }
    
    Write-Host "CLI installed!"
  }
}

function PublishUniversalPackages() {
    try {
      Write-Host "Adding the 'azure-devops' extension..."
      & $env:AzExe extension add --name azure-devops
      Write-Host "'azure-devops' added!"
    }
    catch {
      Write-Host $_
      Write-Host $_.Exception
      Write-Host $_.ScriptStackTrace
    }

  $uniqueBlobs = New-Object System.Collections.Generic.HashSet[string]
  $versionManager = [VersionManager]::new()
  
  foreach ($blobCategory in $BlobCategories.Split(";")) {
    $blobs = Get-ChildItem $ArtifactStagingDirectory -recurse -include "*$blobCategory" 

    foreach ($blob in $blobs) {
      [void]$uniqueBlobs.Add($blob.FullName.ToLower())
    }
  }

  foreach ($blob in $uniqueBlobs) {
    $packageName = Split-Path -Path $blob -Leaf
    $packageVersion = $UniversalPackageVersion
    
    if ($packageVersion -eq $null -or $packageVersion -eq "") {
      if ($IsStable -eq 'true') {
        $packageVersion = "0.0.0"
      } else {
        $packageVersion = $versionManager.GetVersion($packageName)
        if ($packageVersion -eq $null -or $packageVersion -eq "") {
          $packageVersion = "0.0.0"
        }
      }
    }
      
    try {
      Write-Host "Publishing '$packageName' with version '$packageVersion' to '$FeedName'"

      & $env:AzExe artifacts universal publish `
        --organization "https://$Organization.visualstudio.com/" `
        --feed "$FeedName" `
        --name "$packageName" `
        --version "$packageVersion" `
        --path "$blob"
    } 
    catch {
      # Since publishing Universal Packages is still on preview it always throws a warning even in successful cases
      if ($_.Exception.ToString().Contains("WARNING: Universal Packages is currently in preview.")) {
        $LASTEXITCODE = 0
      }
    }
  }
}

try {
  InstallAzureCLI
  PublishUniversalPackages
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
