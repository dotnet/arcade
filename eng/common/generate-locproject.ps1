

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

Push-Location "$env:BUILD_SOURCESDIRECTORY" # push location for Resolve-Path -Relative to work
$resxFiles = Get-ChildItem -Recurse -Path "$env:BUILD_SOURCESDIRECTORY\*\*.resx"

$locJson = @{
    Projects = @(
        @{
            LanguageSet = "Azure_Languages"
            LocItems = @(
                $resxFiles | ForEach-Object {
                    @{
                        SourceFile = $_.FullName
                        CopyOption = "LangIDOnName"
                        OutputPath = "Localize\$(($_.DirectoryName | Resolve-Path -Relative).Substring(2) + "\" + $_.Name)"
                    }
                }
            )
        }
    )
}

$json = ConvertTo-Json $locJson -Depth 5
Write-Host "LocProject.json generated:`n`n$json`n`n"
Pop-Location

New-Item "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject.json" -Force
Set-Content "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject.json" $json