# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

$ErrorActionPreference = 'Stop'

if (-not $env:HELIX_WORKITEM_UPLOAD_ROOT) {
    throw 'HELIX_WORKITEM_UPLOAD_ROOT is required'
}

$sourceRoot = (Get-Location).ProviderPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
$uploadRoot = [IO.Path]::GetFullPath($env:HELIX_WORKITEM_UPLOAD_ROOT).TrimEnd([IO.Path]::DirectorySeparatorChar)
New-Item -ItemType Directory -Path $uploadRoot -Force | Out-Null

$files = @(Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | Where-Object {
    $_.Name -match '(\.trx|testResults\.xml|test-results\.xml|test_results\.xml|junit-results\.xml|junitresults\.xml)$' -and
    -not $_.FullName.StartsWith($uploadRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
})

foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($sourceRoot.Length).TrimStart([IO.Path]::DirectorySeparatorChar)
    $destination = Join-Path $uploadRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
