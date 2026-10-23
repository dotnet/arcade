# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

. $PSScriptRoot\..\get-build-manifest-artifacts.ps1

Describe 'BAR manifest discovery' {
  It 'preserves legacy manifest and release config artifacts' {
    $result = Get-BuildManifestArtifacts @{ assets = @(@{ name = 'assets/manifests/sdk/20261013.1/MergedManifest.xml' }) }
    $result.ManifestArtifact | Should Be 'AssetManifests'
    $result.ReleaseConfigsArtifact | Should Be 'ReleaseConfigs'
    $result.IsVariant | Should Be $false
  }

  It 'preserves assetless and legacy builds without archived manifests' {
    $result = Get-BuildManifestArtifacts @{ assets = @() }
    $result.ManifestArtifact | Should Be 'AssetManifests'
    $result.IsVariant | Should Be $false
  }

  It 'selects each BAR records own variant from the same pipeline run' {
    foreach ($variant in @('daily', 'preview')) {
      $build = @{
        azureDevOpsBuildId = 123
        assets = @(
          @{ name = "dotnetup/0.2.0-$variant.1/dotnetup-win-x64.exe" },
          @{ name = "assets/manifests/sdk/20261013.1/variants/$variant/MergedManifest.xml" }
        )
      }
      $result = Get-BuildManifestArtifacts $build
      $result.ManifestArtifact | Should Be "AssetManifests_$variant"
      $result.ReleaseConfigsArtifact | Should Be "ReleaseConfigs_$variant"
      $result.IsVariant | Should Be $true
    }
  }

  It 'rejects ambiguous manifests rather than publishing the wrong candidate' {
    $build = @{ assets = @(
      @{ name = 'assets/manifests/sdk/20261013.1/variants/daily/MergedManifest.xml' },
      @{ name = 'assets/manifests/sdk/20261013.1/variants/preview/MergedManifest.xml' }
    ) }
    { Get-BuildManifestArtifacts $build } | Should Throw
  }

  It 'rejects invalid variant names' {
    $build = @{ assets = @(@{ name = 'assets/manifests/sdk/20261013.1/variants/preview*/MergedManifest.xml' }) }
    { Get-BuildManifestArtifacts $build } | Should Throw
  }

  It 'rejects malformed BAR assets' {
    $build = @{ assets = @(@{ name = $null }) }
    { Get-BuildManifestArtifacts $build } | Should Throw
  }
}
