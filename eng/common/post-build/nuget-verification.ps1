<#
.SYNOPSIS
    Verifies NuGet packages using dotnet nuget verify.
.DESCRIPTION
    Initializes the .NET CLI and runs 'dotnet nuget verify' on the provided NuGet packages. 
    This script writes an error if any of the provided packages fail verification.
.PARAMETER args
    Package paths to verify, passed directly to 'dotnet nuget verify'.
.EXAMPLE
    PS> .\nuget-verification.ps1 *.nupkg
    Verifies all .nupkg files in the current working directory.
#>

[CmdletBinding(PositionalBinding = $false)]
param(
   [Parameter(ValueFromRemainingArguments = $true)]
   [string[]]$args
)

# `tools.ps1` checks $ci to perform some actions. Since the post-build
# scripts don't necessarily execute in the same agent that run the
# build.ps1/sh script this variable isn't automatically set.
$ci = $true
$disableConfigureToolsetImport = $true
. $PSScriptRoot\..\tools.ps1

$fence = New-Object -TypeName string -ArgumentList '=', 80

# Initialize the dotnet CLI
$dotnetRoot = InitializeDotNetCli -install:$true
$dotnet = Join-Path $dotnetRoot (GetExecutableFileName 'dotnet')

Write-Host "Using dotnet: $dotnet"
Write-Host " "

# Execute dotnet nuget verify
Write-Host "Executing dotnet nuget verify..."
Write-Host $fence
& $dotnet nuget verify $args
Write-Host $fence
Write-Host " "

# Respond to the exit code.
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet nuget verify found some problems."
} else {
    Write-Output "dotnet nuget verify succeeded."
}
