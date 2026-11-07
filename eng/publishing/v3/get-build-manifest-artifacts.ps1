# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

function Get-BuildManifestArtifacts {
  param([Parameter(Mandatory = $true)] $BuildInfo)

  $manifests = @(foreach ($asset in $BuildInfo.assets) {
    if ($asset.name -isnot [string] -or [string]::IsNullOrWhiteSpace($asset.name)) {
      throw "BAR build contains an asset without a name."
    }
    if ($asset.name.StartsWith('assets/manifests/', [StringComparison]::Ordinal) -and
        $asset.name.EndsWith('/MergedManifest.xml', [StringComparison]::Ordinal)) {
      $asset
    }
  })
  if ($manifests.Count -gt 1) {
    throw "BAR build contains more than one archived merged manifest."
  }

  $variant = ''
  if ($manifests.Count -eq 1 -and $manifests[0].name -match '/variants/([^/]+)/MergedManifest\.xml$') {
    $variant = $Matches[1]
    if ($variant -cnotmatch '^[A-Za-z0-9_]+$') {
      throw "Invalid build variant in archived manifest ID."
    }
  }

  if ($variant -ne '') {
    return @{
      ManifestArtifact = "AssetManifests_$variant"
      ReleaseConfigsArtifact = "ReleaseConfigs_$variant"
      IsVariant = $true
    }
  }
  return @{
    ManifestArtifact = 'AssetManifests'
    ReleaseConfigsArtifact = 'ReleaseConfigs'
    IsVariant = $false
  }
}
