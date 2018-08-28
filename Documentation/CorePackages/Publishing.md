# Arcade SDK Publishing Implementation

The publishing logic used by the Arcade SDK is implemented [here](../src/Microsoft.DotNet.Arcade.Sdk/tools/Publishing.proj) and [here](../src/Microsoft.DotNet.Build.Tasks.Feed). This document provides a brief outline of how to use the implementation and how it works.

Arcade onboarded repos use this implementation automatically by using the Arcade SDK. *If the repo use the SDK you won't have to do anything else to use the publishing implementation.*

The main entry point for the publishing implementation is the `Publish` target, which is invoked from within the `Execute` target from [Build.proj](../../src/Microsoft.DotNet.Arcade.Sdk/tools/Build.proj) during the build. The `Publish` target is able to publish to Azure and Source Build local folders. The logic includes the publishing of regular and symbol packages. If no symbol package is found for a specific package the regular package is duplicated and assumed to be a symbol package.

The [PushToBlobFeed](../../src/Microsoft.DotNet.Build.Tasks.Feed/src/PushToBlobFeed.cs) task is used to publish the packages to Azure. Below is a list of optional parameters that control the logic. These parameters are handled by the Arcade SDK and forwarded to the `Publish` target during publishing. You can find more documentation about some of the parameters mentioned below on the [ArcadeSdk documentation](../ArcadeSdk.md).

#### Publish Parameters

| Parameter                     | Description                                                  |
| ----------------------------- | ------------------------------------------------------------ |
| DotNetPublishBlobFeedUrl      | Target Azure feed URL. If empty no publishing will be made to Azure. |
| DotNetPublishBlobFeedKey      | Azure account key.                                           |
| DotNetOutputBlobFeedDir       | Source Build publishing directory. If empty no publishing will occur to Source build local directory. |
| DotNetSymbolServerTokenMsdl   | Personal access token for MSDL symbol server. Available from variable group DotNet-Symbol-Publish. |
| DotNetSymbolServerTokenSymWeb | Personal access token for SymWeb symbol server. Available from variable group DotNet-Symbol-Publish. |
| DotNetSymbolExpirationInDays  | Symbol expiration time in days (defaults to 10 years).       |



## The PushToBlobFeed Task

The `PushToBlobFeed` task is used by the publishing logic to push packages to feed containers. By default this task will publish symbol packages as flat containers and regular packages as non-flat containers. However, to maintain consistency across repos on how we publish our assets, *we encourage that you publish all your packages as flat containers*. For that, make sure that the property `PublishFlatContainer` is set to `true`. This task is also able to create a manifest file with the list of packages/blobs that were published.

Below is a list of the parameters accepted by this task.

#### PushToBlobFeed Parameters

** Parameters marked in bold are required.

| Parameter Name              | Description                                                  |
| --------------------------- | ------------------------------------------------------------ |
| **ExpectedFeedUrl**         | Blob feed URL. I.e., link to index.json file.                |
| **AccountKey**              | Key to access the feed.                                      |
| **ItemsToPush**             | ItemGroup listing the packages to be published.              |
| Overwrite                   | Set to *True* if overriding packages is allowed. Defaults to *false*. |
| PassIfExistingItemIdentical | Pass if existing package is byte-for-byte identical. Used when Overwrite is false. *Default is false.* |
| PublishFlatContainer        | True if this is a flat container. *By default only symbol packages will be published as flag containers.* If this parameter is true regular and symbol packages will be published as flat containers. |
| MaxClients                  | Number of packages to publish in parallel. *Default is 8*.   |
| SkipCreateContainer         | Set to *true* if the container shouldn't be created. Default is *false*. |
| UploadTimeoutInMinutes      | Timeout in minutes for individual package upload. *Default is 5*. |
| AssetManifestPath           | Path to where to store build manifest file.                  |
| ManifestBuildData           | Additional key=value attributes to append to build manifest file. |
| ManifestRepoUri             | URI of the repo producing the build.                         |
| ManifestBuildId             | Build Id that produced the packages.                         |
| ManifestBranch              | Repository branch currently being built.                     |
| ManifestCommit              | Last commit before the build.                                |
