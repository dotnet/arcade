function Test-FilesUseTelemetryOutput {
    $requireTelemetryExcludeFiles = @(
        "enable-cross-org-publishing.ps1",
        "performance-setup.ps1" )

    $filesMissingTelemetry = Get-ChildItem -File -Recurse -Path $engCommonRoot -Include "*.ps1" -Exclude $requireTelemetryExcludeFiles |
        Where-Object { -Not( $_ | Select-String -Pattern "Write-PipelineTelemetryError" )}

    If($filesMissingTelemetry) {
        Write-PipelineTelemetryError -category 'Build' 'One or more eng/common scripts do not use telemetry categorization.'        Write-Host "See https://github.com/dotnet/arcade/blob/master/eng/common/pipeline-logging-functions.ps1"
        Write-Host "The following ps1 files do not include telemetry categorization output:"
        ForEach($file In $filesMissingTelemetry) {
            Write-Host $file
        }

        return 1
    }
}

$engCommonRoot = Join-Path $PSScriptRoot "common"
$failOnConfigureToolsetError = $true
$exitCode = Test-FilesUseTelemetryOutput
return $exitCode