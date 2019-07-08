# YAML Stages Based Publishing and Validation

This document describes the new YAML based approach that will be used for build artifact publishing.  This applies for builds from public branches, as well as for internal branches, with a few additional considerations.

## What are YAML stages?

Stages are a concept introduced by Azure DevOps to organize the jobs in a pipeline.  Just like Jobs are a collection of Steps, Stages can be thought about as a collection of Jobs, where for example, the same pipeline can be split into Build, Test, Publishing, and deployment stages.
Stages are the way that Azure DevOps is bringing build and release pipelines together, and are going to be the replacement for the RM UI based release pipelines.
The official documentation for YAML Stages can be found [here](https://docs.microsoft.com/en-us/azure/devops/pipelines/process/stages?view=azure-devops&tabs=yaml)

## Why use YAML stages for publishing?

Using stages for publishing seeks to unify the Arcade SDK build artifact publishing mechanisms into a single solution that brings together the advantages of both the in-build synchronous publishing and the release pipeline based asynchronous publishing approaches.

* Clearly separate the concepts of build, publishing and validation(s)
* Support publishing/validation related errors to be reported in the same UI as the build errors.
* Additional stages can be added after the publishing has happened, allowing for extensibility to the default arcade publishing.

## How to onboard onto YAML based publishing

In order to use this new publishing mechanism, the easiest way to start is by making your existing build pipeline a single stage, and adding a second stage that is driven by a template distributed with Arcade.

1. Disable package publishing during the build:

    Set the `enablePublishUsingPipelines` template parameter to `true` when calling the `/eng/common/templates/jobs/jobs.yml` template.

    ```YAML
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
      parameters:
        enablePublishUsingPipelines: true
    ```

    It is recommended to use the jobs.yml template to manage this property, as it will make sure to flow it to all the jobs and steps that require it.  The template also handles a lot of the boilerplate steps that need to be performed during the build, such as the publishing of build assets to the BAR. More information about the jobs.yml template can be found [here](../AzureDevOps/PhaseToJobSchemaChange.md#--what-is-the-engcommontemplatesjobsjobsyml-template) If it is not possible for the repo to use the jobs.yml template, the following steps need to be performed:

    * Set a variable called `_PublishUsingPipelines` with a value of `true` so that the gathering of asset manifests won't be performed during the build.

      ```YAML
      variables:
      - name: _PublishUsingPipelines
        value: true
        ...
      ```

    * Set the `publishUsingPipelines` parameter to true when calling the [publish-build-assets.yml](../../eng/common/templates/job/publish-build-assets.yml) template.

      ```YAML
      - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
        - template: /eng/common/templates/job/publish-build-assets.yml
          parameters:
            ...
            publishUsingPipelines: true
            ...
      ```

1. Add the stages keyword to your existing pipeline's YAML:

    ```YAML
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
      parameters:
        enablePublishUsingPipelines: true
    ...
    ```

    becomes

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

1. Add the new [post-build.yml](../../eng/common/templates/post-build/post-build.yml) arcade template as a separate stage for official builds

    ```YAML
    - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
    - template: eng\common\templates\post-build\post-build.yml
        parameters:
        enableSymbolValidation: false
    ```

    The parameters for the template are the following:

    | Name                         | Type     | Description                                                   |Default Value     |
    | -----------------------------| -------- | ------------------------------------------------------------- |----- |
    | enableSourceLinkValidation   | bool     | Run sourcelink validation during the post-build stage.        | true |
    | enableSigningValidation      | bool     | Run signing validation during the post-build stage.           | true |
    | enableSymbolValidation       | bool     | Run symbol validation during the post-build stage.            | true |
    | enableNugetValidation        | bool     | Run NuGet package validation tool during the post build stage.| true |
    | SDLValidationParameters      | object   | Parameters for the SDL job template, as documented in the [SDL template documentation](../HowToAddSDLRunToPipeline.md) | -- |

    Examples of the use of stages can be found in the Arcade family of repos:

    * [Arcade](https://github.com/dotnet/arcade/blob/master/azure-pipelines.yml)
    * [Arcade-Validation](https://github.com/dotnet/arcade-validation/blob/master/azure-pipelines.yml)
    * [Arcade-Services](https://github.com/dotnet/arcade-services/blob/master/azure-pipelines.yml)

1. Once the post-build template is added, a repo's official build will include a series of stages that will publish to the different available feeds, depending on the BAR default channel that the build will be assigned to.  For more information on channels, see the [Channels, Branches and Subscriptions document](../BranchesChannelsAndSubscriptions.md).

    **Note:** At the moment, triggering stages manually is not supported by Azure DevOps. Once this capability is in place, builds will be able to publish to additional channels besided the default.

    The pipeline for a build with stages enabled will look similar to this:

    ![build-with-post-build-stages](./images/build-with-post-build-stages.png)

## Additional considerations for Internal and stable builds

In order to publish stable packages, or those meant for internal servicing releases, require some additional setup so that publishing, dependency flow and package restore work correctly.

### Builds from internal/ branches

Packages from internal/ branches will not be published to public feeds, but will instead be published to a private transport feed. In order to be able to restore packages from these feeds, repos will need to add the feed to both their NuGet.config and eng\versions.props files.

### Stable builds

For stable builds, where every build will produce packages with the same version, the publishing pipeline will generate a new package feed and publish the packages there. In order to be able to restore these packages, Maestro++ will flow the package feeds required to restore any packages located in any such feeds into the repo's NuGet.config as part of a dependency update PR.

```XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <!--Begin: Package sources managed by Dependency Flow automation. Do not edit the sources below.-->
    <add key="darc-int-dotnet-arcade" value="<private-feed-containing the packages>" />
    <!--End: Package sources managed by Dependency Flow automation. Do not edit the sources above.-->
    <add key="arcade" value="https://dotnetfeed.blob.core.windows.net/dotnet-tools-internal/index.json" />
    <add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

The PackageSources enclosed in the `<!--Begin: Package sources managed by Dependency Flow automation. Do not edit the sources below.-->` and `<!--End: Package sources managed by Dependency Flow automation. Do not edit the sources above.-->` comments are managed by automation, and will be added and removed by dependency update pull requests as they are needed.
