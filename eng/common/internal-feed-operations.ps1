param(
  [Parameter(Mandatory=$true)][string] $Operation,
  [string] $AuthToken,
  [string] $CommitSha,
  [string] $RepoName,
  [switch] $IsFeedPrivate
)

. $PSScriptRoot\tools.ps1

function SetupCredProvider{
  param(
    [string] $AuthToken
  )    

  # Install the Cred Provider NuGet plugin
  Write-Host "Setting up Cred Provider NuGet plugin in the agent..."...
  Write-Host "Getting 'installcredprovider.ps1' from 'https://github.com/microsoft/artifacts-credprovider'..."

  $url = 'https://raw.githubusercontent.com/microsoft/artifacts-credprovider/master/helpers/installcredprovider.ps1'
  
  Write-Host "Writing the contents of 'installcredprovider.ps1' locally..."
  Invoke-WebRequest $url -OutFile installcredprovider.ps1
  
  Write-Host "Installing plugin..."
  .\installcredprovider.ps1 -Force
  
  Write-Host "Deleting local copy of 'installcredprovider.ps1'..."
  Remove-Item .\installcredprovider.ps1

  if (-Not("$env:USERPROFILE\.nuget\plugins\netcore")) {
    Write-Host "CredProvider plugin was not installed correctly!"
    ExitWithExitCode 1  
  } 
  else {
    Write-Host "CredProvider plugin was installed correctly!"
  }

  # Then, we set the 'VSS_NUGET_EXTERNAL_FEED_ENDPOINTS' environment variable to restore from the stable 
  # feeds successfully

  $nugetConfigPath = "$RepoRoot\NuGet.config"

  if (-Not (Test-Path -Path $nugetConfigPath)) {
    Write-Host "NuGet.config file not found in repo's root!"
    ExitWithExitCode 1  
  }
  
  $endpoints = New-Object System.Collections.ArrayList
  $nugetConfigPackageSources = Select-Xml -Path $nugetConfigPath -XPath "//packageSources/add[contains(@key, 'darc-')]/@value" | foreach{$_.Node.Value}
  
  if (($nugetConfigPackageSources | Measure-Object).Count -gt 0 ) {
    foreach ($stableRestoreResource in $nugetConfigPackageSources) {
      $trimmedResource = ([string]$stableRestoreResource).Trim()
      [void]$endpoints.Add(@{endpoint="$trimmedResource"; password="$AuthToken"}) 
    }
  }

  if (($endpoints | Measure-Object).Count -gt 0) {
      # Create the JSON object. It should look like '{"endpointCredentials": [{"endpoint":"http://example.index.json", "username":"optional", "password":"accesstoken"}]}'
      $endpointCredentials = @{endpointCredentials=$endpoints} | ConvertTo-Json -Compress
      $restoreProjPath = "$PSScriptRoot\restore.proj"

      # Create the environment variables de AzDo way
      Write-LoggingCommand -Area 'task' -Event 'setvariable' -Data $endpointCredentials -Properties @{
        'variable' = 'VSS_NUGET_EXTERNAL_FEED_ENDPOINTS'
        'issecret' = 'false'
      } 

      # We don't want sessions cached since we will be updating the endpoints quite frequently
      Write-LoggingCommand -Area 'task' -Event 'setvariable' -Data 'False' -Properties @{
        'variable' = 'NUGET_CREDENTIALPROVIDER_SESSIONTOKENCACHE_ENABLED'
        'issecret' = 'false'
      } 

      '<Project Sdk="Microsoft.DotNet.Arcade.Sdk"/>' | Out-File "$restoreProjPath"
  }
  else
  {
    Write-Host "No internal endpoints found in NuGet.config"
  }
}

#Workaround for https://github.com/microsoft/msbuild/issues/4430
function InstallDotNetSdkAndRestoreArcade {
  $dotnetTempDir = "$RepoRoot\dotnet"
  $dotnetSdkVersion="2.1.507"
  $dotnet = "$dotnetTempDir\dotnet.exe"
  $restoreProjPath = "$PSScriptRoot\restore.proj"
  
  Write-Host "Installing dotnet SDK version $dotnetSdkVersion to restore Arcade SDK..."
  InstallDotNetSdk "$dotnetTempDir" "$dotnetSdkVersion"

  & $dotnet restore $restoreProjPath

  Write-Host "Arcade SDK restored!"

  if (Test-Path -Path $restoreProjPath) {
    Remove-Item $restoreProjPath
  }

  if (Test-Path -Path $dotnetTempDir) {
    Remove-Item $dotnetTempDir -Recurse
  }
}

try {
  Push-Location $PSScriptRoot

  if ($Operation -like "setup") {
    SetupCredProvider $AuthToken
  } 
  elseif ($Operation -like "install-restore") {
    InstallDotNetSdkAndRestoreArcade
  }
  else {
    Write-Host "Unknown operation '$Operation'!"
    ExitWithExitCode 1  
  }
} 
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
} 
finally {
    Pop-Location
}