# Asynchronous Publishing and Releasing

## What is Asynchronous Publishing?

The plan for Asynchronous Publishing is [described in this text](https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/AsyncPublishing.md). The current document describes the overall implementation and how to use it. 

Asynchronous Publishing is *a new mechanism for publishing build artifacts (including symbols) to different forms of feeds*. The big difference between the old publishing mechanism and the new mechanism is that in the latter publishing is decoupled from the build. In other words, with the new mechanism, pushing artifacts to a feed is not a part of the build itself anymore, instead, it happens as a consequence of building and assigning an intent to the build (a Maestro++ Channel).

This new mechanism is implemented on top of Azure DevOps release pipelines and integrated with Maestro++. With the introduction of Asynchronous Publishing, channels in Maestro++ can have one or more AzDO release pipelines associated with them. Whenever a build finishes successfully and gets pushed to Maestro++ it gets associated to one or more channels. Whenever a build is associated to a channel all pipelines associated with that channel are started with the current build as source.

In this new mechanism publishing will happen during execution of the release pipelines associated with the channel the build got assigned to. Different pipelines, or the same pipeline associated with different channels, can publish to different locations (e.g., MyGet, NuGet.org, Azure, etc.) 

## Which problem does it solve? Why should I care about this?

The pipelines provide an extension point where many different processes can be applied to artifacts before & during publishing. The current implementation provides the following benefits:

- **Work around the [feed lock problem](https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/AsyncPublishing.md#problems):** since publishing isn't part of the build anymore, they won't timeout due to long wait times for the feed lock.
- **Reduce build times:** since publishing was moved out of the build process build times can considerably decrease.

In the coming weeks / months we'll work to bring the following processes into the flow:

- Symbol validation - tracking issue: [#1874](https://github.com/dotnet/arcade/issues/1874) [#173](https://github.com/dotnet/arcade/issues/173)
- Signing validation - [#1444](https://github.com/dotnet/arcade/issues/1444)

## How do I use it?

The mechanism currently is opt-in, you just need to set two build variables to start using it. Assuming the repository is up to date with Arcade build templates and uses the latest Arcade SDK, you just need to:

Set the `enablePublishUsingPipelines` parameter of `/eng/common/templates/jobs/job.yml` to true:

```xml
jobs:
- template: /eng/common/templates/jobs/jobs.yml
  parameters:
    ...
    enablePublishUsingPipelines: true
```

Pass these two new properties to `eng\common\cibuild.cmd` :

```xml
	/p:DotNetPublishUsingPipelines=true
	/p:DotNetArtifactsCategory=.NETCore
```

This will let the build templates and Maestro++ know that the build is onboarded with publishing using pipelines.

We suggest that the value of these properties/parameters be set as variables in your main build file. Check one of these build files for a reference:

- [Arcade](https://github.com/dotnet/arcade/blob/master/azure-pipelines.yml)
- [Arcade-Validation](https://github.com/dotnet/arcade-validation/blob/master/azure-pipelines.yml)
- [Arcade-Services](https://github.com/dotnet/arcade-services/blob/master/azure-pipelines.yml)

## How do I know where the artifacts will be published?

The place where artifacts will be published depend on which channel(s) the build was assigned to. Currently we are pushing to two different feeds - we'll soon add more. Builds assigned to channel `.NET Tools - Validation` have their artifacts published to the [arcade-validation feed](https://dotnetfeed.blob.core.windows.net/arcade-validation/index.json), builds assigned to other channels get their artifacts published to [dotnet-core feed](https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json).

**Note 1:** builds can be assigned to more than one channel.

**Note 2:** Maestro++ is configured to assign a default channel to builds produced from official branches, like master, production, etc. Currently there is no automatic channel assignment for builds produced from private/personal branches. To see how to publish these artifacts read further in this document.

## How do I know if publishing has succeeded?

Maestro++ should automatically create a new release for each pipeline associated for each channel of the build.  The build's artifacts will have been published once a release associated with the build have finished executing.

The release should be created within a few minutes after the build finishes. If it takes much more than that, something probably went wrong. We are working on a notification mechanism to alert the user when publishing failed for some reason; [here is the tracking issue](https://github.com/dotnet/arcade/issues/2303).

Currently we have only one release pipeline serving all channels: [Public Dev Release pipeline](https://dnceng.visualstudio.com/internal/_release?definitionId=36).

**Note 1:** since we are using one pipeline for all channels you can just look up if there is a release matching your build among the releases produced from the [Public Dev Release Pipeline](https://dnceng.visualstudio.com/internal/_release?definitionId=36).

## How to publish artifacts from builds of private/personal branches?

To publish these artifacts you'll need to start the release manually. Just go to the release pipeline that you want to use and create a new release. In case of the Public Dev Release Pipeline it will ask you for two parameters:

- Add the build that produced the artifacts as a Artifact Source for the pipeline.

- Inform the BAR Build ID of the build. This can be obtained following the example below:

  1) Go to *Publish to Build Asset Registry*:

  ![Publish-to-BAR](images\Publish-to-BAR.png)

  2) Then go to *Publish Build Assets* step of the build:

  ![BarBuildID](images\BarBuildID.png)
