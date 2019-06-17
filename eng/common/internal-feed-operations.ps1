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
  $nugetConfigPackageSources = Select-Xml -Path $nugetConfigPath -XPath "//packageSources/add[contains(@key, 'darc-int-')]/@value" | foreach{$_.Node.Value}
  
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

      #Workaround for https://github.com/microsoft/msbuild/issues/4430
      $dotnetTempDir = "$RepoRoot\dotnet"
      $dotnetSdkVersion="2.1.507"
      $dotnet = "$dotnetTempDir\dotnet.exe"

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
  else
  {
    Write-Host "No internal endpoints found in NuGet.config"
  }
}

function CreateNewFeed {
  param(
    [switch] $IsFeedPrivate,
    [string] $AuthToken,
    [string] $RepoName,
    [string] $CommitSha
  )  

  Write-Host $IsFeedPrivate
  if ($IsFeedPrivate) {
    $feedsUrl = 'https://feeds.dev.azure.com/dnceng/_apis/packaging/feeds'
    $feedName = "darc-int-$RepoName-$CommitSha"

    Write-Host "Creating new feed '$feedName' in '$feedsUrl'"
  
    # Mimic the permissions added to a feed when created in the browser
    $permissions = New-Object System.Collections.ArrayList
    [void]$permissions.Add(@{identityDescriptor="Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8"; role=3});
    [void]$permissions.Add(@{identityDescriptor="Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293"; role=3});
    [void]$permissions.Add(@{identityDescriptor="Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1349140002-2196814402-2899064621-3782482097-0-0-0-0-1"; role=4});
    [void]$permissions.Add(@{identityDescriptor="Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1846651262-2896117056-2992157471-3474698899-1-2052915359-1158038602-2757432096-2854636005"; role=4});

    $body = @{name=$feedName;permissions=$permissions} | ConvertTo-Json -Compress

    $bytes = [System.Text.Encoding]::ASCII.GetBytes(":$AuthToken")
    $encodedToken = [Convert]::ToBase64String($bytes)
    $headers = @{"Accept"="application/json; api-version=5.0-preview.1"; "Content-type"="application/json"; "Authorization"="Basic $encodedToken"}
    
    Invoke-WebRequest $feedsUrl -Body $body -Headers $headers -Method 'POST'
  }
  else {
  #Write-Host $IsFeedPrivate
   # Public feeds haven't GA'ed yet and we don't know yet how they work. 
  }
}

try {
  Push-Location $PSScriptRoot

  if ($Operation -like "setup") {
    SetupCredProvider $AuthToken
  } 
  elseif ($Operation -like "create-feed") {
    if ($RepoName -eq "" -or $CommitSha -eq "") {
      Write-Host "-RepoName and -CommitSha are required for a 'create-feed' operation!"
      ExitWithExitCode 1  
    }

    if ($IsFeedPrivate -and $AuthToken -eq "") {
      Write-Host "-AuthToken is required for a private feed creation!"
      ExitWithExitCode 1  
    }

    CreateNewFeed -IsFeedPrivate $IsFeedPrivate -AuthToken $AuthToken -RepoName $RepoName -CommitSha $CommitSha
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