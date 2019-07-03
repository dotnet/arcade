# YAML Stages Based Publishing

This document describes the new Yaml based approach that will be used for package publishing.  This applies for builds from public branches, as well as for internal branches, with a few additional considerations.

## What are YAML stages?

Stages are a concept introduced by Azure DevOps to organize the jobs in a pipeline.  Just like Jobs are a collection of Steps, Stages can be thought about as a collection of Jobs, where for example, the same pipeline can be split into Build, Test, Publishing, and deployment stages.
Stages are the way that Azure DevOps is bringing build and release pipelines together, and are going to be the replacement for the RM UI based release pipelines.
The official documentation for YAML Stages can be found [here](https://docs.microsoft.com/en-us/azure/devops/pipelines/process/stages?view=azure-devops&tabs=yaml)

## Why use YAML stages for publishing?

Using stages for publishing seeks to unify the Arcade SDK build artifact publishing mechanisms into a single solution that brings together the advantages of both the in-build synchronous publishing and the release pipeline based asynchronous publishing approaches.

* Publishing is decoupled from the build. It's easier to reason about build failures as oposed to publishing failures.
* All the information about a build is available in a single location. All stages run as part of the same Pipeline definition, so there is no need to go to a separate Release Pipeline to reason about it.
* Additional stages can be added after the publishing has happened, allowing for extensibility to the default arcade publishing.

## How to onboard onto YAML based publishing

In order to use this new publishing mechanism, the easiest way to start is by making your existing build pipeline a single stage, and adding a second stage that is driven by a template distributed with Arcade.

1. Add the stages keyword to your existing pipeline's YAML:

    ```YAML
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
    ...
    ```

    becomes

    ```YAML
    stages:
    - stage: build
    displayName: Build
    jobs:
    - template: /eng/common/templates/jobs/jobs.yml
    ...
    ```

2. Add the new post-build arcade template as a separate stage for official builds

    ```YAML
    - ${{ if and(ne(variables['System.TeamProject'], 'public'), notin(variables['Build.Reason'], 'PullRequest')) }}:
    - template: eng\common\templates\post-build\post-build.yml
        parameters:
        enableSymbolValidation: false
        enableSourceLinkValidation: false
        SDLValidationParameters:
            enable: false
    ```

    The parameters for the template are the following:

    | Name                         | Type     | Description                                                  |
    | -----------------------------| -------- | ------------------------------------------------------------ |
    | enableSourceLinkValidation   | bool     | Run sourcelink validation during the post-build stage.       |
    | enableSigningValidation      | bool     | Run signing validation during the post-build stage.          |
    | enableSymbolValidation       | bool     | Run symbol validation during the post-build stage.           |
    | enableNugetValidation        | bool     | Run NuGet package validation tool during the post build stage.|
    | SDLValidationParameters      | bool     | Parameters for the SDL job template, as documented in the [SDL template documentation](../HowToAddSDLRunToPipeline.md) |

    Examples of the use of stages can be found in the Arcade family of repos:

    * [Arcade](https://github.com/dotnet/arcade/blob/master/azure-pipelines.yml)
    * [Arcade-Validation](https://github.com/dotnet/arcade-validation/blob/master/azure-pipelines.yml)
    * [Arcade-Services](https://github.com/dotnet/arcade-services/blob/master/azure-pipelines.yml)

3. Once the post-build template is added, a repo's official build will include a series of stages contain a series of jobs to publish to the different available feeds, depending on the Maestro++ channel that the build is assigned to.  For more information on channels, see the [Channels, Branches and Subscriptions document](../BranchesChannelsAndSubscriptions.md).

    **Note:** At the moment, triggering stages manually is not supported by Azure DevOps. We are relying on the default channels that a build should be assigned to in order to determine which publishing stages should be triggered.

    The pipeline for a build with stages enabled will similar to this:

    ![build-with-post-build-stages](images/build-with-post-build-stages.png)

## Additional considerations for Internal and stable builds

In order to publish packages meant for internal servicing or that wish to produce stable packages require some additional setup so that publishing, dependency flow and package restore work correctly.

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
