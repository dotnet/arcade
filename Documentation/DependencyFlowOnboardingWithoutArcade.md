# Dependency Flow Onboarding for Repos Not on Arcade

## Feed package

The [Microsoft.DotNet.Build.Tasks.Feed](https://github.com/dotnet/arcade/tree/master/src/Microsoft.DotNet.Build.Tasks.Feed) package (available from the dotnet-core feed - `https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json`) contains an MSBuild task which will [generate a manifest](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Build.Tasks.Feed/src/GenerateBuildManifest.cs) for you.

The preferred path, is that teams use the Feed package to publish packages to blob storage, this model will additionally just create a manifest for you (assuming you specify the additional [manifest values](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Build.Tasks.Feed/build/Microsoft.DotNet.Build.Tasks.Feed.targets#L32)).  If you do not want to use the Feed package for package publishing, then you can still use the Feed package to generate a manifest.

## Generate a manifest

Assuming tht you're not using the Feed package for publishing, your repo, there are a number of ways that you can generate a manifest.  This sample is not the exclusive way to generate a manifest and your repos layout or pre-reqs may mandate a different method.

### Manifest Generation Example

Here is one example (using MSBuild 15.0) of generating a manifest by using the Arcade SDK (it is not necessary that your repo itself build using the Arcade SDK).

File layout

```TEXT
\publish
  -global.json
  -NuGet.config
  -eng\GenerateBuildManifest.props
  -eng\common\sdk-task.ps1
  -eng\common\tools.ps1
```

`eng\common\sdk-task.ps1` and `eng\common\tools.ps1` are available from the [Arcade repo](https://github.com/dotnet/arcade/tree/master/eng/common), there are corresponding `sh` files if you need to run on Unix.

global.json

```JSON
{
  "msbuild-sdks": {
    "Microsoft.DotNet.Build.Tasks.Feed": "2.2.0-beta.19151.1"
  }
}
```

NuGet.config

```XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <solution>
    <add key="disableSourceControlIntegration" value="true" />
  </solution>
  <packageSources>
    <clear />
    <add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
</configuration>
```

GenerateBuildManifest.props

```XML
<?xml version="1.0" encoding="utf-8"?>
<Project>
  <ItemGroup>
    <SymbolPackages Include="$(ArtifactsShippingPackagesDir)*.symbols.nupkg" IsShipping="true" />
    <SymbolPackages Include="$(ArtifactsNonShippingPackagesDir)*.symbols.nupkg" IsShipping="false" />

    <PackagesToPublish Include="$(ArtifactsShippingPackagesDir)*.nupkg" IsShipping="true" />
    <PackagesToPublish Include="$(ArtifactsNonShippingPackagesDir)*.nupkg" IsShipping="false" />
    <PackagesToPublish Remove="@(ExistingSymbolPackages)" />
  </ItemGroup>

  <ItemGroup>
    <ItemsToPush Include="@(PackagesToPublish);@(ExistingSymbolPackages);@(SymbolPackagesToGenerate)">
      <ManifestArtifactData Condition="'%(IsShipping)' != 'true'">NonShipping=true</ManifestArtifactData>
    </ItemsToPush>
  </ItemGroup>
</Project>
```

Generate a manifest

If all of your packages are "shipping" packages, you can just specify the `PackagesToPublishPattern` on the command-line and you do not need to include the "GenerateBuildManifest.props" file mentioned above...

> `powershell -ExecutionPolicy Bypass -Command "eng\common\sdk-task.ps1 -restore -task GenerateBuildManifest /p:PackagesToPublishPattern=e:\gh\chcosta\arcade\artifacts\packages\Debug\NonShipping\*.nupkg /p:AssetManifestFilePath=e:\gh\chcosta\feed2\manifest.xml"`

For more control over your assets, you can exclude the `PackagesToPublishPattern` option from the command-line but include "GenerateBuildManifest.props" in your repo.  This will allow you to specify packages that are shipping vs non-shipping (shipping is the default).  More details about shipping are included [here](https://github.com/dotnet/arcade/blob/b0c930c2b44acd03671552f52b925183db0fc8ea/Documentation/Darc.md#gathering-a-build-drop).

> `powershell -ExecutionPolicy Bypass -Command "eng\common\sdk-task.ps1 -restore -task GenerateBuildManifest /p:AssetManifestFilePath=e:\gh\chcosta\feed2\manifest.xml"`

## Publish the manifest to BAR

### Publishing a single manifest

If you only have one Azure DevOps job that publishes assets, then you can add this [task](https://github.com/dotnet/arcade/blob/de44b15e79b9d124d04c16458bead2a1d7ea02ef/eng/common/templates/job/publish-build-assets.yml#L47) into your build steps.

Publish manifest to BAR

> `powershell -ExecutionPolicy Bypass -Command "eng\common\sdk-task.ps1 -task PublishBuildAssets -restore -msbuildEngine dotnet /p:ManifestsPath='$(Build.StagingDirectory)/Download/AssetManifests' /p:BuildAssetRegistryToken=$(MaestroAccessToken) /p:MaestroApiEndpoint=https://maestro-prod.westus2.cloudapp.azure.com"`

`MaestroAccessToken` is available by referencing the "Publish-Build-Assets" [variable group](https://github.com/dotnet/arcade/blob/de44b15e79b9d124d04c16458bead2a1d7ea02ef/eng/common/templates/job/publish-build-assets.yml#L36) in dnceng/internal.

### Publishing multiple manifests

If you have multiple jobs that publish assets, then you need to publish the generated manifests from each of those legs.  In Arcade, this is done by publishing each of the manifests as Artifacts to Azure DevOps and then having a [final job](https://github.com/dotnet/arcade/blob/de44b15e79b9d124d04c16458bead2a1d7ea02ef/eng/common/templates/job/publish-build-assets.yml) that runs and downloads the manifests / publishes them all to BAR