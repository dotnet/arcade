# Transport Feed

## Why Transport Feed
More than 15% of our build failures are due to MyGet errors. These are both restore and publish errors and have ranged from network connectivity to inability to push/locate packages already on the feed due to service issues.
To address these issues, the idea is to remove our dependency on MyGet as a part of official builds. 

## Requirements
 * Needs to support packages, symbols and other build assets 
 * Needs to have a configurable retention policy plan
 * Needs to be based on the NuGet protocol
 
## Solution
Utilise Azure blob storage as the backing storage. A participent repo's official build will push it's build output to a single feed. 

## How it works
```
   Typical Feed url - https://<STORAGEACCOUNT>.blob.core.windows.net/<CONTAINER>/<RELATIVEPATH>/index.json
```
Transport feeds have the same file layout that a NuGet v3 feed would typically use in blob storage. This consists of a root service index.json file, which contains the base url of the feed. Each package that lives in the feed has its own package name folders and each of these folders have versions inside them. Each package folder has an index.json, which contains all the versions that the package supports. These package index.json are generated during the push step.

We upload the nupkg, then generate packages index.jsons on disk then upload and overwrite the package index.json. The generation of the index.jsons is to support NuGet v3. These blob feeds currently don't support the nuget.exe install operation.

## Structure of the feed inside the Storage Account

```
CONTAINER: dotnet-core
    |
    +-- packages
    |   +-- index.json
    |   +-- Package1
    |   |   +-- Version1
    |   |       +-- Package1.Version1.nupkg
    |   |   +-- Version2
    |   |       +-- Package1.Version2.nupkg
    |   |   +-- index.json
    |   +-- Package2
    |       +-- Version1
    |       |   +-- Package2.Version1.nupkg
    |       +-- index.json
    +-- symbols
    |   +-- Package1
    |   |   +-- Version1
    |   |   |   +-- Package1.Version1.symbols.nupkg
    |   |   +-- Version2
    |   |   |   +-- Package1.Version2.symbols.nupkg
    |   +-- Package2
    |       +-- Version1
    |           +-- Package2.Version1.symbols.nupkg
    +-- assets
        +-- Installer.msi
        +-- Installer.deb
        +-- Installer.rpm
        +-- BuildOutput.zip
        +-- BuildOutput.tar.gz
```

## Moving to Transport Feed

The current transport feed for Dotnet Core is https://dotnetcore.blob.windows.net/dotnet-core/packages/index.json. This is publically accessible, meaning anyone can restore the packages.

### Publishing: 
 To resolve the publish errors, begin pushing build output to the transport feed. This requires a dependency Microsoft.Dotnet.Build.Tasks.Feed package which lives in buildtools. The repos must create a step in their build specific to publishing and use the PushToBlobFeed task in the tool.
 
 To use PushToBlobFeed,
 

1. Add a reference to the latest Microsoft.DotNet.Build.Tasks.Feed which lives in the buildtools myget feed (https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json) and restore to a known location.

2. Decide the feed to which you would like to push. (the storage account, container, and relativepath)

    ```
       Typical Public Feed Url - https://<STORAGEACCOUNT>.blob.core.windows.net/<CONTAINER>/<RELATIVEPATH>/index.json
    ```
    
3. Create a publish msbuild project that includes the custom PushToBlobFeed.
    ```
      <UsingTask TaskName="PushToBlobFeed" AssemblyFile="D:\repo\(net46/netstandard1.5)\Microsoft.DotNet.Build.Tasks.Feed.dll"/>
    ```
    
4. Construct an ItemGroup of packages you want to push to your transport feed
    ```
    <ItemGroup>
        <ItemsToPush Include="$(MSBuildThisFileDirectory)/Release/**/*.nupkg" />
    </ItemGroup>
    ```

5.  Add the call to PushToBlobFeed
    ```
    <PushToBlobFeed ExpectedFeedUrl="Your Feed Url"
                    AccountKey="Your Storage Account Key"
                    ItemsToPush="@(ItemsToPush)" />
    ```
    If you have assets to push without create a NuGet Feed in flat, set PublishFlatContainer=true. 
    If you would like to overwrite, set Overwrite=true.

6.  Build the project.
    ```
       msbuild or dotnet msbuild <publish project> 
    ```

### Consumption:
 To resolve the restore errors, move external nuget and myget build dependencies into the transport feed and restore from the transport feed. This is set via your NuGet.config or the sources specified during restore.

If you are using dotnet restore, use the --source parameter to specify the feed.

If you do not have an explicit call, you can create a target prior to the RestorePackages step and modify NugetSources Property.

ConfigureInputFeed is another utility task from Microsoft.Dotnet.Build.Tasks.Feed.dll that will generate a NuGet.config with the feed url in the folder specified.

To use ConfigureInputFeed,

1. Configure sources in an ItemGroup
   ```
    <ItemGroup>
      <!-- Specify the sources you would like enabled/disabled -->
      <EnableFeeds Include="https://karajascli.blob.core.windows.net/karthikfeed-12349/packages/index.json" />
      <EnableFeeds Include="https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json" />
      <DisableFeeds Include="https://dotnet.myget.org/F/dotnet-core/api/v3/index.json" />
    </ItemGroup>

2. Add the call to ConfigureInputFeed
   ```
     <ConfigureInputFeed EnableFeeds="@(EnableFeeds)" />
   ```

## FAQ

1. What happens when we need to update an external package dependency?

You'll need to manually push the package to the transport feed. The general process to follow is sync all your dependencies locally and then use PushToBlobFeed to push them to the feed.

2. Does the transport feed support overwriting?

Yes. You can overwrite any package or asset uploaded.

3. Does transport feed support private secure feeds?

Yes. This is currently supported and requires a proxy to be stood up. Please contact dnceng.