param(
  [Parameter(Mandatory=$true)][string] $InputPath,              # Full path to directory where artifact packages are stored
  [Parameter(Mandatory=$true)][string] $ExtractPath            # Full path to directory where the packages will be extracted
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$disableConfigureToolsetImport = $true

function ExtractArtifacts {
  if (!(Test-Path $InputPath)) {
    Write-Host "Input Path does not exist: $InputPath"
    ExitWithExitCode 0
  }
  $ArchivePathHelper = Join-Path $PSScriptRoot 'archive-extraction-path.ps1'
  $Jobs = @()
  Get-ChildItem "$InputPath\*.nupkg" |
    ForEach-Object {
      $Jobs += Start-Job -ScriptBlock $ExtractPackage -ArgumentList $_.FullName, $ExtractPath, $ArchivePathHelper
    }

  if ($Jobs.Count -eq 0) {
    return
  }

  Wait-Job -Job $Jobs | Out-Null
  foreach ($Job in $Jobs) {
    Receive-Job -Job $Job -ErrorAction Stop
  }
}

try {
  # `tools.ps1` checks $ci to perform some actions. Since the SDL
  # scripts don't necessarily execute in the same agent that run the
  # build.ps1/sh script this variable isn't automatically set.
  $ci = $true
  . $PSScriptRoot\..\tools.ps1

  $ExtractPackage = {
    param(
      [string] $PackagePath,                                # Full path to a NuGet package
      [string] $ExtractionRoot,                             # Full path to extraction root
      [string] $ArchivePathHelper                           # Archive path validation helper
    )

    if (!(Test-Path $PackagePath)) {
      throw "Input file does not exist: $PackagePath"
    }

    . $ArchivePathHelper

    $RelevantExtensions = @('.dll', '.exe', '.pdb')
    Write-Host -NoNewLine 'Extracting ' ([System.IO.Path]::GetFileName($PackagePath)) '...'

    $PackageId = [System.IO.Path]::GetFileNameWithoutExtension($PackagePath)
    $PackageExtractPath = Get-ValidatedArchiveEntryPath `
      -ExtractionRoot $ExtractionRoot `
      -EntryName $PackageId

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = $null
    try {
      $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

      $ExtractionPlan = @(
        $zip.Entries |
          ForEach-Object {
            [PSCustomObject]@{
              Entry = $_
              TargetFile = Get-ValidatedArchiveEntryPath `
                -ExtractionRoot $PackageExtractPath `
                -EntryName $_.FullName
            }
          }
      )

      [System.IO.Directory]::CreateDirectory($PackageExtractPath) | Out-Null
      $ExtractionPlan |
        Where-Object {$RelevantExtensions -contains [System.IO.Path]::GetExtension($_.Entry.Name)} |
          ForEach-Object {
            [System.IO.Directory]::CreateDirectory(
              [System.IO.Path]::GetDirectoryName($_.TargetFile)) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($_.Entry, $_.TargetFile)
          }
    }
    finally {
      if ($null -ne $zip) {
        $zip.Dispose()
      }
    }
  }
  Measure-Command { ExtractArtifacts }
}
catch {
  Write-Host $_
  Write-PipelineTelemetryError -Force -Category 'Sdl' -Message $_
  ExitWithExitCode 1
}
