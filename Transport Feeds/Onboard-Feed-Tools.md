# Onboarding to Transport Feeds

To address the large number of MyGet restore failures, we propose that we move a repo build to use transport feeds as a part of restore. A transport feed is an NuGet feed hosted in Azure Blob storage.
You can use Microsoft.Dotnet.Build.Tasks.Feed tools to restore from transport feeds of other repos and you can also use it to publish your packages to a transport feed for consumption by other repos.

## Steps publish to a transport feed

1. Restore Microsoft.Dotnet.Build.Tasks.Feed: 
   * Add a reference to Microsoft.DotNet.Build.Tasks.Feed which lives in the buildtools myget feed (https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json)
   * After restore, create a folder with unpacked targets and dlls from the package.
   * Here is a modified init tools that does the same as an example https://github.com/dotnet/corefx/commit/eab07d217ff60e92da482ba0fce8c4cd23e14293

2. Import Publish.targets into your publishing project file. 
    ```
        <Import Project="$(MSBuildThisFileDirectory)<Your unpack folder>/Publish.targets" />  
    ```
    * If you don't have a publishing project file there is a sample here https://github.com/karajas/corefx/blob/7997d54eece3367f18a50c11fd3353384e3522ef/src/publish.proj
    * Here is another example Example: https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Transport%20Feed/repopublish.targets
    * The ItemGroup PublishDependsOn can be modified to create the list of targets you want finished before you invoke PublishOutputLeg.

3. Construct an ItemGroup of packages you want to push to your transport feed
    ```
    <ItemGroup>
        <ItemsToPush Include="$(MSBuildThisFileDirectory)/Release/**/*.nupkg" />
    </ItemGroup>
    ```
    
    Decide where in the container you would like your feed to live by specifying the RelativePath property.
    ```
    <!-- This will create your feed at https://yourfeed.blob.core.windows.net/yourcontainer/packages/index.json 
         For symbols you may want to specify RelativePath as symbols
    -->
    <PropertyGroup>
        <RelativePath>packages</RelativePath>
    </PropertyGroup>
    ```
    If you have assets to push without create a NuGet Feed, set PublishFlatContainer=true. You can overwrite them by passing in Overwrite.

4.  Set Feed parameters
     You will need to set the following properties. You can specify them at build time on the command line.
     
     * AccountName - The storage account in your Azure subscription
     * AccountKey - The password from your Azure storage account from above
     * ContainerName - Container in which the feed is created
     * ItemsToPush - items to publish to feed
     * RelativePath - the relative path in the container where the feed is created.
     
    Optional parameters:
     * PublishFlatContainer - Allows you to push a flat file to a container, useful for non-nupkg packages
     * Overwrite - Allows you to overwrite packages (potentially useful for maintaining version numbers)

4. During your publish step in your build definition, invoke the PublishOutputLeg target on your publish project.
    ```
       dotnet msbuild <publish project> /t:PublishOutputLeg
    ```
    
## Steps to use feed tools to configure input feeds
    
1. This will generate a NuGet.config in the folder specified by the property RepoRoot (default is the current directory)

   Before the sync or restore step in your build, you can invoke the ConfigureInputFeed target with the following ItemGroups set.
   Example: https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Transport%20Feed/reporestore.targets
   
   ```
    <ItemGroup>
      <!-- Specify the sources you would like enabled/disabled -->
      <EnableFeeds Include="https://karajascli.blob.core.windows.net/karthikfeed-12349/packages/index.json" />
      <EnableFeeds Include="https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json" />
    </ItemGroup>
   ```
   
   To note, currently all transport feeds are unauthenticated, so you can restore from them publicly.
   
   You can use the publish tools above to move all your package dependencies to a transport feed if you'd like to move off of MyGet completely.