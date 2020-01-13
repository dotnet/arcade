param(
  [Parameter(Mandatory=$true)][int] $BARBuildId,
  [Parameter(Mandatory=$true)][int] $PromoteToMaestroChannelId,
  [Parameter(Mandatory=$true)][string] $MaestroApiAccessToken,
  [Parameter(Mandatory=$false)][string] $MaestroApiEndPoint = 'https://maestro-prod.westus2.cloudapp.azure.com',
  [Parameter(Mandatory=$false)][string] $MaestroApiVersion = '2019-01-16'
)

try {
  . $PSScriptRoot\post-build-utils.ps1

  if ($PromoteToMaestroChannelId -eq 0) {
    $Content = Get-Content ${Env:Build_StagingDirectory}/ReleaseConfigs/ReleaseConfigs.txt

    $BarId = $Content | Select -Index 0

    $Channels = ""
    $Content | Select -Index 1 | ForEach-Object { $Channels += "$_ ," }

    $IsStableBuild = $Content | Select -Index 2

    $AzDOProjectName = $Env:System_TeamProject
    $AzDOPipelineId = $Env:System_DefinitionId
    $AzDOBuildId = $Env:Build_BuildId	
  }
  else {
    $buildInfo = Get-MaestroBuild -BuildId $BARBuildId

    if (!$buildInfo) {
      Write-PipelineTelemetryError -Category 'PromoteBuild' -Message "Build with BAR ID $BARBuildId was not found in BAR!"
      ExitWithExitCode 1
    }

    $BarId = $buildInfo.Id
    $Channels = ""
    $IsStableBuild = "False" #TODO: Fix this once this issue is done: https://github.com/dotnet/arcade/issues/3834

    $AzDOProjectName = $buildInfo.azureDevOpsProject
    $AzDOPipelineId = $buildInfo.azureDevOpsBuildDefinitionId
    $AzDOBuildId = $buildInfo.azureDevOpsBuildId

    write-host $buildInfo
  }

  Write-Host "##vso[task.setvariable variable=BARBuildId;isOutput=true]$BarId"
  Write-Host "##vso[task.setvariable variable=InitialChannels;isOutput=true]$Channels"
  Write-Host "##vso[task.setvariable variable=IsStableBuild;isOutput=true]$IsStableBuild"

  Write-Host "##vso[task.setvariable variable=AzDOProjectName;isOutput=true]$AzDOProjectName"
  Write-Host "##vso[task.setvariable variable=AzDOPipelineId;isOutput=true]$AzDOPipelineId"
  Write-Host "##vso[task.setvariable variable=AzDOBuildId;isOutput=true]$AzDOBuildId"
  Write-Host "##vso[task.setvariable variable=PromoteToMaestroChannelId;isOutput=true]$PromoteToMaestroChannelId"
}
catch {
  Write-Host $_.ScriptStackTrace
  Write-PipelineTelemetryError -Category 'TriggerSubscriptions' -Message $_
  ExitWithExitCode 1
}
