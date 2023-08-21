param(
  [Parameter(Mandatory=$true)][string] $InputPath,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$tokensToRedact
)

try {
  . $PSScriptRoot\post-build-utils.ps1

  $packageName = 'MSBuild.BinlogRedactor.CLI'

  $dotnetRoot = InitializeDotNetCli -install:$true
  $dotnet = "$dotnetRoot\dotnet.exe"
  $toolList = & "$dotnet" tool list -g

  if ($toolList -like "*$packageName*") {
    & "$dotnet" tool uninstall $packageName -g
  }

  $packageFeed = 'https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json'

  $toolPath  = "$TempDir\logredactor\$(New-Guid)"
  $verbosity = 'minimal'
  
  New-Item -ItemType Directory -Force -Path $toolPath
  
  Push-Location -Path $toolPath

  try {
    Write-Host "Installing Binlog redactor CLI..."
    Write-Host "'$dotnet' new tool-manifest"
    & "$dotnet" new tool-manifest
    Write-Host "'$dotnet' tool install $packageName --prerelease --add-source '$packageFeed' -v $verbosity"
    & "$dotnet" tool install $packageName --local --prerelease --add-source "$packageFeed" -v $verbosity
  

    $optionalParams = [System.Collections.ArrayList]::new()
  
    Foreach ($p in $tokensToRedact)
    {
	  if($p -match '^\$\(.*\)$')
	  {
		Write-Host ("Ignoring token {0} as it is probably unexpanded AzDO variable"  -f $p)
	  }		  
	  elseif($p)
	  {
        $optionalParams.Add("-p") | Out-Null
	    $optionalParams.Add($p) | Out-Null
	  }
    }

    & $dotnet redact-binlog -f -r -i $InputPath `
	  @optionalParams

    if ($LastExitCode -ne 0) {
      Write-Host "Problems using Redactor tool. But ingoring them now."
    }
  }
  finally {
    Pop-Location
  }

  Write-Host 'done.'
} 
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Category 'Redactor' -Message "There was an error while trying to redact logs."
  ExitWithExitCode 1
}