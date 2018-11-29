# Asynchronous Publishing and Releasing in .NET Core 3
This document describes changes to publishing to support asynchronicity and ready the engineering system for future servicing of .NET Core 3.

## Current State
Currently, publishing is done in one of two ways by each repo:
- **Use of Arcade to do publishing** - Repository selects what kinds of assets need publishing (e.g. symbols, flat files, packages), provides targets and keys for the output locations (e.g. blob feed URL + storage key) and Arcade's publish package handles the ferrying of outputs to the target locations. During this publishing, Arcade records a manifest of the outputs. At the final join point in the build (e.g. Windows and Linux are both done), the build joins all manifests and uploads the build information to the Build Asset Registry (BAR). 
- **Existing non-Arcade publishing tasks** - Repository uses some existing tasks (e.g. from buildtools, or custom within the repo's build infrastructure) and publishes what it needs to various feeds and output storage accounts. The outputs are not tracked.

Typically these publishing steps are done unconditionally on each official build.

## Problems
The two approaches above are roughly equivalent, with more tracability and integration into the Maestro/BAR in the Arcade approach. Over time as repositories onboard onto Arcade, it is expected that non-standard publishing workflows will disappear. However, there are still significant issues with the current Arcade approach.
- **Blob feed publishing is lock heavy** - Most repositories are using a single feed target for blob feed publishing (https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json). The tooling that mimics nuget pushes on top of Azure storage requires a lock when pushing. When multiple builds are attempting to push to the same feed at the same time, only one may push at a time. This leads to significant wait times to finish publishing, potentially causing builds to time out.

  *It should be noted that this particular issue is somewhat point-in-time. When anonymous feeds are available on Azure DevOps, we can switch to using those instead, and lock contention will no longer be an issue*
- **Outputs are always public** - Because most repositories are pushing to public endpoints (e.g. dotnetfeed, myget, etc.) on every build, the onus is on the maintainer of the build or queuer of a manual build to understand the right buttons or switches to press (if available) in cases where outputs should not be made public. For example:
    - Security releases cannot be made public until day of release. They cannot be placed on public output locations.
    - Stabilized (non-suffixed) builds need to be built to isolated locations so they can be iterated upon before final release. Pushing to non-isolated public locations invalidates the ability to iterate because overwrite rules are often strict.
- **Build intention must be known prior to build** - Because most outputs are being published publicly by default during the build, repo owners are essentially declaring the intent of a build at the time of the build. Every build is a public dev build. This is fundamentally wrong, and inconsistent with how we ship software.

## Goals
The improved publishing story has the following goals:
- **Publishing is largely lock free** - Build completion should not be blocked on other builds' actions.
- **Publishing is fast** - Reduce much of the publishing time in builds by moving expensive steps elsewhere. For example, core-setup's publishing steps to myget and the dotnet-core blob feed take about 40 minutes, about as long as the rest of the build.
- **Outputs are isolated** - Build outputs should not initially mix with outputs of other builds.
- **Outputs don't always need to be public** - For security, stabilized, or other internal builds, final publishing should happen to internal locations, rather than public locations.
- **Publishing steps can vary based on build intent (assigned channel)** - .NET Core 3 models build intent by assigned channel. Depending on what channel a build is assigned to, publishing steps can vary. Publishing happens when build intent is declared (channel assignment).
- **All build outputs are private by default**

## Final Workflow
The final desired workflow is intended to utilize a few key pieces of infrastructure to achieve the stated goals:
- Azure DevOps release pipelines to perform release activities
- BAR/Maestro for release pipeline triggering
- Isolated blob feeds (eventually Azure DevOps feeds + additional artifact storage) for per-build storage

The final workflow is as follows:
1. Each leg of a build that wants to publish outputs uses the arcade publishing package to push outputs. These outputs are sent to an intermediate, isolated and private storage feed (see below for options). As outputs are pushed, the package records metadata about each item in a manifest which is also pushed for later use. The manifest identifies any such information that may be needed to perform subsequent release activities. This manifest should look much like it does today, with some tweaks:
    - **Include** - Which bits 'ship' vs. those which are just inter-repo transport.
    - **Include** - Categories/generalized names for output locations to be interpreted by the release pipelines. Examples:
        - 'SDK blob storage'
        - 'SDK checksum storage'
        - '.NET Core Nuget Package'
    - **Do not include** - Specific storage accounts or resources. For example, the manifest should not identify the specific 'dotnetcli' storage account as the target for blobs, as an internal CLI release will want to push to an internal dev release account, while a public dev release will push to the dotnetcli storage account. These details should be abstracted in a way that allows for interpretation based on the *intent* of the build.
2. At the finalization join point in the build, the BAR/Maestro build upload happens. This joins the manifests from various legs into one, then provides that information to Maestro/BAR. Initially, the build assets locations are recorded as their intermediate locations.
3. Maestro/BAR associates the build with a specific channel. This can happen either automatically, via a default repo+branch->channel mapping, or manually for a specific build by a user.
4. Each channel may have a release pipeline associated with it. If a pipeline is associated with the channel, Maestro/BAR triggers the release pipeline associated with the channel on the build that was just assigned to the channel. Maestro waits until this release is done to do any additional actions, like subscription updates.
5. The release pipeline associated with the new build uses the artifacts from the build (one of which is the manifest) to process and push the outputs of this build to appropriate locations. At the end of the release pipeline, new outputs locations (e.g. myget, nuget, etc.) and potentially new assets (e.g. checksums) are added to the build's assets. *Note: These new assets can be applied to subscription updates as subscription actions will not have taken place until the build is through the release pipeline.*
6. Any time a new channel is associated with a build, a release pipeline for that channel may be run. This allows for the promotion of builds through channels (e.g. internal dev build -> public release build)

*Note: This plan allows for a backup release strategy, where a release pipeline may be triggered on a build manually by a user. The same release pipeline would work with or without the Maestro, though additional output locations may not be recorded*

### Options for intermediate feeds
Intermediate feeds used for the build could take a number of forms:
- Blob Feeds
    - Advantages - Can be used as a nuget feed. Can serve flat files. Long retention times.
    - Disadvantages - Not implicitly associated with a build. Auth can be clunky (SAS tokens/storage account keys)
- Azure DevOps build storage
    - Advantages - Implicitly associated with a specific build. Built in auth (AD auth). Can be accessed from UI.
    - Disadvantages - Can't be used as a nuget feed. Retention is limited.

Today blob feeds are used as intermediate storage. We should transition to using Azure DevOps build storage. Because Azure DevOps allows for identification of artificats during the build using artifact logging commands (https://github.com/Microsoft/azure-pipelines-tasks/blob/master/docs/authoring/commands.md#artifact-logging-commands) this transition can be made transparent to a repository.

## Implementation Roadmap
It is an explicit goal to make this transition to a new system of publishing as painless as possible. For typical user scenarios, developers should notice little difference in day to day operations. Required repository changes should be minimized or eliminated if possible. The implementation of this new system should proceed in phases.

### Phase 1 - Remove lock contention and introduce public dev release pipelines for feeds only
The first phase is intended to remove the "Waiting for exclusive lock on the feed" issues seen in the builds, as well as set up for phase 2. To implement this phase, the following steps will be taken:

1. Introduce an Azure DevOps release pipeline concept into Maestro/Bar. Release pipelines are associated with a channel. Upon assignment of a build to a channel, if that channel has an associated release pipeline, the release pipeline should be triggered for the build. Execution of additional post-build actions (e.g. subscription updates) will block on the completion of the release pipeline.
2. Introduce the ability for build assets to have multiple locations, and for those locations to be added to (additive only) at the end of the release pipeline. Additionally, new assets may be added.
3. Create a 'public dev release' Azure DevOps pipeline which pushes outputs from a build to a blob feed based on the build manifest. For now, the manifest should record that the current ExpectedFeedUrl is the target of the release pipeline, and the source is the intermediate feed used. At the end of the release pipeline, the BAR should be updated with the new asset locations.
4. Change PushToBlobFeed to upload package artifacts to Azure DevOps build storage rather than ExpectedFeedUrl. Initially, only nuget packages should be pushed. Using artifact logging commands (https://github.com/Microsoft/azure-pipelines-tasks/blob/master/docs/authoring/commands.md#artifact-logging-commands) this should be doable without disruptive changes in the repository. The initial upload to the BAR should note the direct link to each package within Azure DevOps build storage.

At the end of this, when a build is done, it pushes its nuget packages to the private intermediate feed, then uploads to BAR as usual. As the build associates with a channel, the associated release pipeline will run, pushing the outputs to the final feed (typically dotnet-core blob feed). Developers will not see "Waiting for exclusive lock on the feed" except in cases where multiple legs of the same build are publishing to the intermediate feed at the same time. This is relatively rare.

### Phase 2 - Move additional publishing steps into public dev release pipelines
In the second phase, we will move the rest of existing publishing (blobs and symbols) for day to day development into the public dev release. This involves:

1. Altering the public dev release pipeline to enable publishing of symbols to mdsl, symweb, as well as regular blobs (e.g. installers). We should use existing arcade tasks do this.
2. Altering the arcade publishing package to stop explicit publishing of those items during the build, instead moving these to push to intermediate storage (Azure DevOps build storage) first. Examples:
    - Remove PublishSymbols call from publish.proj
    - Alter PushToBlobFeed to do appropriate things when pushing flat files. Flat files should go to the intermediate storage first, then to final locations (categories of output locations are noted in the manifest, actual output locations are defined by the release pipeline).

### Phase 3 - Introduce additional piplines for internal and stable releases
In the third phase, we will introduce additional release pipelines for other scenarios:
- Internal only - Push to common, authenticated Azure DevOps feed rather than regular dotnet-core feed. Output blob storage should only be a private common location.
- Support for stable only (may be internal) - Push to predictable locations that can be deleted and recreated as necessary. The analog to this today would be stabilized ProdCon v1 blob feeds, which are based on a "product build id". The feed can be deleted and recreated so that packages with identical versions can be re-uploaded. Whether or not a build is stable can be noted by build tags, which can be set within the build based on parameters using Azure DevOps logging commands (https://github.com/Microsoft/azure-pipelines-tasks/blob/master/docs/authoring/commands.md).
