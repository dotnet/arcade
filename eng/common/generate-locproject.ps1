

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

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
                        OutputPath = "Localize\$($_.DirectoryName + "\" + $_.Name)"
                    }
                }
            )
        }
    )
}

$json = ConvertTo-Json $locJson -Depth 5
Write-Host "LocProject.json generated:`n`n$json`n`n"

New-Item ".\Localize\LocProject.json"
Set-Content ".\Localize\LocProject.json" $json