Param(
    [string] $LanguageSet = 'VS_Main_Languages',        # Language set to be used in the LocProject.json
    [string] $CreateFile = "true",                      # Creates a LocProject.json when "true," otherwise generates a LocProject.json and compares it to one that already exists in the repo 
    [string] $CreateNeutralXlfs = "true"                # Creates neutral xlf files. Only set to false when running locally
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$exclusionsFilePath = "$env:BUILD_SOURCESDIRECTORY\Localize\LocExclusions.json"
$exclusions = @{ Exclusions = @() }
if (Test-Path -Path $exclusionsFilePath)
{
    $exclusions = Get-Content "$env:BUILD_SOURCESDIRECTORY\Localize\LocExclusions.json" | ConvertFrom-Json
}

Push-Location "$env:BUILD_SOURCESDIRECTORY" # push location for Resolve-Path -Relative to work
$jsonFiles = Get-ChildItem -Recurse -Path "$env:BUILD_SOURCESDIRECTORY" | Where-Object { $_.FullName -Match "en\\strings.json" }
$xlfFiles = @()

$allXlfFiles = Get-ChildItem -Recurse -Path "$env:BUILD_SOURCESDIRECTORY\*\*.xlf"
$langXlfFiles = @()
if ($allXlfFiles.Length -gt 0) {
    $isMatch = $allXlfFiles[0].FullName -Match "\.([\w-]+)\.xlf" # matches '[langcode].xlf'
    $firstLangCode = $Matches.1
    $langXlfFiles = Get-ChildItem -Recurse -Path "$env:BUILD_SOURCESDIRECTORY\*\*.$firstLangCode.xlf"
}
$langXlfFiles | ForEach-Object {
    $isMatch = $_.Name -Match "([^.]+)\.[\w-]+\.xlf" # matches '[filename].[langcode].xlf'
    
    $destinationFile = "$($_.Directory.FullName)\$($Matches.1).xlf"
    $xlfFiles += Copy-Item "$($_.FullName)" -Destination $destinationFile -PassThru
}

$locFiles = $jsonFiles + $xlfFiles

$locJson = @{
    Projects = @(
        @{
            LanguageSet = $LanguageSet
            LocItems = @(
                $locFiles | ForEach-Object {
                    $outputPath = "Localize\$(($_.DirectoryName | Resolve-Path -Relative).Substring(2) + "\")" 
                    $continue = $true
                    $exclusions.Exclusions | ForEach-Object {
                        if ($outputPath.Contains($_))
                        {
                            $continue = $false
                        }
                    }
                    if ($continue)
                    {
                        $sourceFile = ($_.FullName | Resolve-Path -Relative).Substring(2)
                        @{
                            SourceFile = $sourceFile
                            CopyOption = "LangIDOnName"
                            OutputPath = $outputPath
                        }
                        if ($CreateNeutralXlfs -ne "true" -and $_.Extension -eq '.xlf') {
                            Remove-Item -Path $sourceFile
                        }
                    }
                }
            )
        }
    )
}

$json = ConvertTo-Json $locJson -Depth 5
Write-Host "(NETCORE_ENGINEERING_TELEMETRY=Build) LocProject.json generated:`n`n$json`n`n"
Pop-Location

if ($CreateFile -eq "true") {
    New-Item "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject.json" -Force
    Set-Content "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject.json" $json
}
else {
    New-Item "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject-generated.json" -Force
    Set-Content "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject-generated.json" $json

    if ((Get-FileHash "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject-generated.json").Hash  -ne (Get-FileHash "$env:BUILD_SOURCESDIRECTORY\Localize\LocProject.json").Hash) {
        Write-Error "Existing LocProject.json differs from generated LocProject.json. Download LocProject-generated.json and compare them."
        
        exit 1
    }
    else {
        Write-Host "Generated LocProject.json and current LocProject.json are identical."
    }
}