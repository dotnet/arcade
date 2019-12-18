# Arcade SDK Publishing Infrastructure

This document describes the infrastructure provided by the Arcade SDK for publishing build artifacts.

## Main differences to previous approaches

- The concept of Channels is central: 

- Publishing happens after the build:

- Don't use Azure DevOps release pipelines: 



## What are YAML stages?

Stages are a concept introduced by Azure DevOps to organize the jobs in a pipeline. Just as Jobs are a
collection of Steps, Stages can be thought of as a collection of Jobs, where for example, the same pipeline can be split into Build, Test and, Publishing stages.

Stages are the way that Azure DevOps is bringing build and release pipelines together, and are going to
be the replacement for the RM UI based release pipelines. The official documentation for YAML-based Stages can be found [here](https://docs.microsoft.com/en-us/azure/devops/pipelines/process/stages?view=azure-devops&tabs=yaml).

## Why use YAML stages for publishing?

Using stages for publishing seeks to unify the Arcade SDK build artifact publishing mechanisms into a single
solution that brings together the advantages of both the in-build synchronous publishing and the previous release pipeline based asynchronous publishing approaches. Other benefits are:

* Clearly separate the concepts of build, test, publish and validate.
* Support publishing and validation errors to be reported in the build page UI.
* Stages can depend on each other, which provides a natural way to extend default Arcade publishing infra with custom (repo or branch specific) publishing steps.

## How to use the new infrastructure

#### Deprecated Parameters

The new infrastructure makes the following parameters useless. These parameters were introduced in previous implementations of the publishing infrastructure; you should not need to pass any of them to the build scripts anymore.

- DotNetPublishToBlobFeed
- DotNetPublishToBlobFeedUrl
- DotNetPublishBlobFeedKey
- DotNetPublishUsingPipelines
- UsingToolSymbolUploader

#### Basic Onboarding Scenario

In order to use this new publishing mechanism, the easiest way to start is by making your existing build pipeline a single stage, and then making use of a YAML template ([eng/common/templates/post-build/post-build.yml](https://github.com/dotnet/arcade/blob/66175ebd3756697a3ca515e16cd5ffddc30582cd/eng/common/templates/post-build/post-build.yml)) provided by Arcade to implement the default publishing stages. Below is the step-by-step process.

1. Update the repo's arcade version to `1.0.0-beta.19360.8` or newer.

1. Add a *top level* variable named `_DotNetArtifactsCategory` to your build-definition YAML. Most repositories will use `.NETCore` as the category unless assets should be published to a feed
    other than the default Maestro managed feeds. See [Advanced Scenarios](##overriding-the-publishing-feed-used-for-builds-in-the-dev-channel) for instructions on how to publish to a specific feeds. See below an example definition:

    ```YAML
    variables:
    ...
    - name: _DotNetArtifactsCategory
      value: .NETCore
    ...
    ```

1. Disable asset publishing during the build. 

    1. If the build job uses the `eng\common\templates\jobs\jobs.yml` template, set the parameter `enablePublishUsingPipelines` to `true`. See example below:

    ```YAML
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
      parameters:
        enablePublishUsingPipelines: true
    ```

    2. If the build job makes direct use of `eng\common\templates\job\job.yml` you'll have to do two changes. The first is to set the `enablePublishUsingPipelines` parameter to `true` when instantiating `job.yml`. The second is to make sure that you use the template `eng\common\templates\job\publish-build-assets.yml` to inform Maestro++ that *all* build jobs have finished executing and passing the parameter `enablePublishUsingPipelines` to `true`. See example below:

      ```YAML
      jobs:
      - template: /eng/common/templates/job/job.yml
        parameters:
          ...
          enablePublishUsingPipelines: true
          ...
      
      - template: /eng/common/templates/job/publish-build-assets.yml
        parameters:
          ...
          publishUsingPipelines: true
          ...
      ```

1. You'll also need to pass the below two MSBuild properties to the build scripts.

  | Name                           | Value                         |
  | ------------------------------ | ----------------------------- |
  | /p:DotNetPublishUsingPipelines | true                          |
  | /p:DotNetArtifactsCategory     | `$(_DotNetArtifactsCategory)` |

    For an example, see how the Arcade repo passes these properties in its [azure-pipelines.yml](https://github.com/dotnet/arcade/blob/2cb8b86c1ca7ff77304f76fe7041135209ab6932/azure-pipelines.yml#L74)


1. Transform your existing build-definition in a stage. Do that by nesting the current job definition(s) under the `stages` keyword. For instance, this example build definition with a single job definition:

    ```YAML
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
      parameters:
        enablePublishUsingPipelines: true
    ...
    ```

    should be transformed to this:

    ```YAML
    stages:
    - stage: build
      displayName: Build
      jobs:
      - template: /eng/common/templates/jobs/jobs.yml
        parameters:
          enablePublishUsingPipelines: true
    ...
    ```

1. Import the new [eng\common\templates\post-build\post-build.yml](../../eng/common/templates/post-build/post-build.yml) Arcade template at the end of the build definition. This will import all default test, validate and publishing stages provided by Arcade. The bottom part of your build definition will look like this:

    ```YAML
    - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
      - template: eng\common\templates\post-build\post-build.yml
        parameters:
          enableSourceLinkValidation: false
          ...
    ```

    The `post-build.yml` template accepts the following parameters:

    | Name                                    | Type     | Description                                                                                          |Default Value |
    | --------------------------------------- | -------- | -----------------------------------------------------------------------------------------------------|----- |
    | enableSourceLinkValidation              | bool     | Run SourceLink validation during the post-build stage.                                               | false |
    | enableSigningValidation                 | bool     | Run signing validation during the post-build stage.                                                  | true |
    | enableNugetValidation                   | bool     | Run NuGet package validation tool during the post build stage.                                       | true |
    | publishInstallersAndChecksums           | bool     | Publish installers packages and checksums from the build artifacts to the dotnetcli storage account. | false |
    | symbolPublishingAdditionalParameters    | string   | Additional arguments for the PublishToSymbolServers sdk task.                                        | '' |
    | artifactsPublishingAdditionalParameters | string   | Additional arguments for the PublishArtifactsInManifest sdk task.                                    | '' |
    | signingValidationAdditionalParameters   | string  | Additional arguments for the SigningValidation sdk task.     |               |
    | SDLValidationParameters                 | object   | Parameters for the SDL job template, as documented in the [SDL template documentation](../HowToAddSDLRunToPipeline.md) | -- |
    | validateDependsOn | [array] | Which stage(s) should the validation stage depend on. | build |
    | publishDependsOn | [array] | Which stage(s) should the publishing stage(s) depend on. | Validate |

    Examples of the use of stages can be found in the Arcade family of repos:

    * [Arcade](https://github.com/dotnet/arcade/blob/master/azure-pipelines.yml)

    * [Arcade-Validation](https://github.com/dotnet/arcade-validation/blob/master/azure-pipelines.yml)

    * [Arcade-Services](https://github.com/dotnet/arcade-services/blob/master/azure-pipelines.yml)

1. Once the post-build template is added, a repo's official build will include a series of stages that will
  publish to the different available feeds, depending on the BAR default channel that the build will be assigned to.  For more information on channels, see the [Channels, Branches and Subscriptions document](../BranchesChannelsAndSubscriptions.md).

  **Note:** At the moment, triggering stages manually is not supported by Azure DevOps. Once this capability  is in place, builds will be able to publish to additional channels besides the default. The Arcade SDK will now publish build artifacts to the Azure DevOps build artifacts instead of publishing to a blob feed directly, and the post-build stages will move the assets in the artifacts to the final feeds depending on the channels that the build is assigned to. The artifacts that the post-build templates use are:

    * PDBArtifacts: Stores pdb files generated by the build.
    * PackageArtifacts: Stores all nupkgs generated by the build.
    * BlobArtifacts: Stores everything else: symbol packages, installers, zip files, etc...

    The pipeline for a build with stages enabled will look similar to this:

    ![build-with-post-build-stages](./images/build-with-post-build-stages.png)

### Validating the changes

Since the post-build stages will only trigger during builds that run in the internal project, there are some
additional steps that need to be performed in order to test that the changes to the pipeline are correct, and that publishing works as expected. We are looking into ways to improve the onboarding experience, and are tracking that through https://github.com/dotnet/arcade/issues/3390

1. Publish a branch to the Azure devops mirror for the repo that includes the pipeline changes
2. Set up a default channel for the internal repo + branch combination using darc that targets the `General Testing` channel. Note that the default channels require the full branch reference.

    ``` Powershell
    # From a repository that contains an eng/common folder
    .\eng\common\darc-init.ps1
    darc add-default-channel --channel ".Net Tools - Validation" --branch "refs/heads/<my_new_branch>" --repo "https://dev.azure.com/dnceng/internal/_git/dotnet-arcade"
    ```

3. Queue a build for your test branch
4. Once the Build stage completes, the validation channel stage should trigger, and publish the packages to the feed during the `Publish Assets` job in the `Validation Channel`

    ![validation-stage](./images/validation-stage.png)

## Advanced Scenarios

### Overriding the publishing feed used for builds in the Dev channel

By default, builds that will be published to the Validation and Dev channels will publish their assets to the
`dotnet-core` feed. The SDK provides a mechanism to override this default for cases where downstream repositories expect the assets to be located in a different feed.

The steps to enable this override are:

1. Create a PR to https://github.com/dotnet/arcade that adds a category for your assets and a target feed
for the category to the TargetStaticFeed PropertyGroup in
[/src/Microsoft.DotNet.Arcade.Sdk/tools/SdkTasks/SetupTargetFeeds.proj](https://github.com/dotnet/arcade/blob/fd91e27589e69c0a97db2e208b112a24ab989180/src/Microsoft.DotNet.Arcade.Sdk/tools/SdkTasks/SetupTargetFeeds.proj)

    ```XML
    <PropertyGroup>
      ...
      <TargetStaticFeed Condition="'$(ArtifactsCategory.ToUpper())' == '.NETCORE'">https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json</TargetStaticFeed>
      <TargetStaticFeed Condition="'$(ArtifactsCategory.ToUpper())' == '.NETCOREVALIDATION'">https://dotnetfeed.blob.core.windows.net/arcade-validation/index.json</TargetStaticFeed>
      ...
      <!-- New Category -->
      <TargetStaticFeed Condition="'$(ArtifactsCategory.ToUpper())' == 'MYNEWCATEGORY'">https://dotnetfeed.blob.core.windows.net/my-feed/index.json</TargetStaticFeed>
      ...
      <TargetStaticFeed Condition="'$(TargetStaticFeed)' == ''">https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json</TargetStaticFeed>
    </PropertyGroup>
    ```

1. Set your new category as the value for the artifact category variables

    ```YAML
    variables:
    ...
    - name: _DotNetValidationArtifactsCategory
      value: MyNewCategory
    - name: _DotNetArtifactsCategory
      value: MyNewCategory
    ...
    ```

### Integrating custom publishing logic

Repositories that perform additional publishing during their build outside of the Arcade SDK publishing should move away from doing so, as they would likely end up publishing to incorrect feeds for servicing builds.

Support for adding truly custom publishing logic to the publish stage in an Arcade-compatible way not yet implemented. Publish steps that can't be accomplished with Arcade must be performed in additional stages that happen before or after the Arcade publish stages.

The Arcade SDK provides a way to convert `PushToBlobFeed` calls: the
[PushToAzureDevOpsArtifacts](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Build.Tasks.Feed/src/PushToAzureDevOpsArtifacts.cs) task generates an appropriately populated asset manifest and publishes the
assets to upload to the build's artifacts. The post-build stages will then publish the assets to the correct location based on the channel that the build will be assigned to and the asset categories.

A conversion to `PushToAzureDevOpsArtifacts` for repos that are using the `PushToBlobFeed` task inside their build would look like this:

1. Replace the `PushToBlobFeed` task with `PushToAzureDevOpsArtifacts`:

    ```XML
    <PropertyGroup>
      <AssetManifestFileName>ManifestFileName.xml</AssetManifestFileName>
      <AssetManifestPath>$(ArtifactsLogDir)AssetManifest\$(AssetManifestFileName)</AssetManifestPath>
    </PropertyGroup>
    
    <PushToBlobFeed
      ExpectedFeedUrl="$(FeedURL)"
      AccountKey="$(FeedKey)"
      ItemsToPush="@(ItemsToPush)"
      ManifestBuildData="Location=$(FeedURL)"
      ManifestRepoUri="$(BUILD_REPOSITORY_URI)"
      ManifestBranch="$(BUILD_SOURCEBRANCH)"
      ManifestBuildId="$(BUILD_BUILDNUMBER)"
      ManifestCommit="$(BUILD_SOURCEVERSION)"
      AssetManifestPath="$(AssetManifestPath)"
      PublishFlatContainer="$(PublishFlatContainer)" />
    ```

    becomes

    ```XML
    <PropertyGroup>
      <AssetManifestFileName>ManifestFileName</AssetManifestFileName>
      <AssetManifestPath>$(ArtifactsLogDir)AssetManifest\$(SdkAssetManifestFileName)</AssetManifestPath>
    
      <!-- Create a temporary directory to store the generated asset manifest by the task -->
      <TempWorkingDirectory>$(ArtifactsDir)\..\AssetsTmpDir\$([System.Guid]::NewGuid())</TempWorkingDirectory>
    </PropertyGroup>
    
    <MakeDir Directories="$(TempWorkingDirectory)"/>
    
    <!-- Generate the asset manifest using the PushToAzureDevOpsArtifacts task -->
    <PushToAzureDevOpsArtifacts
      ItemsToPush="@(ItemsToPush)"
      ManifestBuildData="Location=$(FeedURL)"
      ManifestRepoUri="$(BUILD_REPOSITORY_URI)"
      ManifestBranch="$(BUILD_SOURCEBRANCH)"
      ManifestBuildId="$(BUILD_BUILDNUMBER)"
      ManifestCommit="$(BUILD_SOURCEVERSION)"
      PublishFlatContainer="$(PublishFlatContainer)"
      AssetManifestPath="$(AssetManifestPath)"
      AssetsTemporaryDirectory="$(TempWorkingDirectory)" />
    
    <!-- Copy the generated manifest to the build's artifacts -->
    <Copy
      SourceFiles="$(AssetManifestPath)"
      DestinationFolder="$(TempWorkingDirectory)\$(AssetManifestFileName)" />
    
    <Message
      Text="##vso[artifact.upload containerfolder=AssetManifests;artifactname=AssetManifests]$(TempWorkingDirectory)/$(AssetManifestFileName)"
      Importance="high" />
    ```

    This will do something similar to what the SDK does for its default publishing pipeline, as seen in [publish.proj](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Arcade.Sdk/tools/Publish.proj)


## Additional considerations for internal and stable builds

In order to publish packages for stable and internal servicing releases, some additional setup is required so
that publishing, dependency flow and package restore work correctly.

### Builds from internal/* branches

Packages from internal/ branches will not be published to public feeds, but will instead be published to a
private transport feed provided by @dnceng. In order to be able to restore packages from this feed, repos will need to add the feed to their NuGet.config file.

### Stable builds

For stable builds, where every build will produce packages with the same version, the publishing pipeline will generate a new package feed to publish the build artifacts on every build. In order for dependent repos to be able to restore these packages, Maestro++ will flow the feed information required to restore packages located in any such feeds into the repo's NuGet.config as part of a dependency update PR.

```XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!--Begin: Package sources managed by Dependency Flow automation. Do not edit the sources below.-->
    <add key="darc-int-dotnet-arcade" value="<private-feed-containing the packages>" />
    <!--End: Package sources managed by Dependency Flow automation. Do not edit the sources above.-->
    <add key="arcade" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json" />
    <add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

The PackageSources enclosed in the
`<!--Begin: Package sources managed by Dependency Flow automation. Do not edit the sources below.-->` and
`<!--End: Package sources managed by Dependency Flow automation. Do not edit the sources above.-->` comments are managed by Maestro++ Dependency update PRs, and will be added and removed by dependency update pull requests as they are needed.

