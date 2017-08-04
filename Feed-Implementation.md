# Implementation of Feed Configuration

As a part of the effort of moving our official builds to a more stable source (Transport Feeds hosted in Azure), we require a mechanism to select the desired output feed. This is because we need to continue to support publishing to MyGet for external customers, while publishing to the transport feed for official builds. Because we need to consume what we produce, the same ability to configure feeds is required for input feeds as well. The only constant about the feeds is that they all use the NuGet protocol.

Given the different scenarios listed [here](https://github.com/chcosta/core-eng/blob/package-acquisition/Documentation/Project-Docs/package-acquisition.md) , we would like a way to configure what nuget sources should be used in each case using a dotnet core app. 

## Things to keep in mind

There are two types of dependencies that need to be consumable by a repo.

**Transport dependency:**
 * May/May not be signed
 * direct output of building a .NET Core repository

**Package dependency:**
 * Signed bits
 * External toolsets/nuget packages (eg. perf, code coverage, etc.)

 The .NET Core Product is composed of the following products. Each one has their own official build and feed for assets produced by their build. There are two thigs to keep in mind.
  * We want to allow every repo to support feed configuration independently. They should be able to specify where their outputs intend to go for their customers (both internal and external). 
  * But we also want to allow the feed configuration to support pulling in local files where we want to build the entire stack offline and on a single machine as in the case for Build From Source.
 
**Component Repos Product for Constructing CLI (.NET Core)**
 * core-setup
 * msbuild
 * netcorecli-fsc
 * newtonsoft-json
 * nuget-client
 * roslyn
 * sdk
 * templating
 * vstest
 * websdk
 * xliff-tasks
 * cli-deps-satellites

## Configure Input

The build runs an app (that can target .NET Core 1.1 or 2.0 depending on the repo) that generates the desired root level NuGet.config with the appropriate sources. For build.proj based repos, the repo owner can invoke it inside of a target that runs pre-RestorePackages using the Exec task passing in the following msbuild variables. For build.cmd/sh based repos, it can be run before they invoke restore on all the csproj's or sln files, after parsing the appropriate parameters.

The parameters required

 - InputFeed : 
    This is the input nuget feed, where the transport and package dependencies are restored from. It can be 
   - MyGet - "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json" (Default)
   - VSTS - "https://devdiv.feeds.visualstudio.com/DefaultCollection/_apis/Packaging/Feeds/aeddd8a4-fc4c-4e03-aa3f-fba380f4b8ee/Packages"
   - Blob Storage - "https://dotnetcore.azurewebsites.com/packages"
   - Local file - ""../bin/source-built/"
            
 - InputFeedPassword : Required for VSTS, blob storage

There is only a single feed ever in use. They are fixed i.e. there is no need to track down a feed url or construct one manually.

## Configure Output

The build runs the same app to create a layout and push packages to the configured feed. This is not a required tool for building and running locally. Instead, this will be a step run in the build definition of builds. Parameters required

 - VersionsFile : A list of previous constructed versions. The VersionsRepo contains the current list from official builds.

 - VersionFileAuth : VersionsRepo requires auth
               
 - OutputPathsToParse : A list of paths where build output lives. This includes packages, symbol packages and other artifacts ( installers, metapackages, etc.)

 - OutputFeed : 
    This is the output nuget feed, where the transport and package dependencies are published to. It can be
   - MyGet - "https://dotnet.myget.org/F/dotnet-core/api/v3/index.json"
   - Blob Storage - "https://dotnetcore.azurewebsites.com/packages"
            
 - OutputFeedPassword : Required for MyGet, blob storage

We can gather the outputs and construct the necessary layout. We group all the nupkg into the packages folder, we group the symbols under symbols. For assets, we construct a nuget package with the same build number version as the other packages.

The version of the packages has been passed in at the root level from the build definition, we just need to update the versions file.

**Behavior of various feeds**

 * MyGet
   * use NuGet push api to push to the feed
          
 * Blob
   * use NuGet add api to create the layout that appears in the feed (with the package index.json)
   * collect version of every package
   * pull down index.json for every package in matching path location and update versions
   * push to blob storage the packages and the new index.json's
   
There will be a potential race condition here if multiple builds are queued up, to avoid this we will lease each blob that we require during the build and wait for a limited amount of time before retrying.
   
 * VSTS
   * use NuGet push api to push to the feed
    
 * File
   * only in source-build context
   
**Possible api spec**

```
    interface IFeed<T>
    {
        //check if key works and service status is running
        bool CheckFeedStatus(string feed);

        // sanitize items to perform feed action on.
        // check if items are de-duplicated, have the right assembly versions and are signed, prior to publishing.
        IEnumerable<T> ValidateItems(IEnumerable<T> items);
    }
```

```
    interface IFeedAction
    {
        //push item to feed
        
        //pull item from feed - with a filter

        //delete package from feed

        //list items in feed - with versions
    }
```