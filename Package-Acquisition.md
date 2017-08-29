# Design of Transport Feeds

## Why Transport Feeds
More than 15% of our build failures are due to MyGet errors. To address these issues, the idea is to remove our dependency on MyGet as a part of official builds. These issues have ranged from network connectivity to inability to push/locate packages already on the feed.

## Requirements
 * The ability to support packages, symbols and assets (installers, metapkg, ...)
 * Configure a retention policy (Long term storage / no storage limits) (we don't have one for MyGet)
 * Removing the dependency on a third party service, which we can't service ourselves 
 * Not having to run another service ourselves
 * Nuget protocol based transport in line with the goals from Build From Source
 * Have the implementation work with Build From Source
 * Needs to allow transport of unsigned bits.
 * Ability to track download numbers of different packages
 
## Current Proposal
The current idea here is to utilise Azure blob storage. The following would apply to every repo that subscribes to this method of publishing. For every official build of a repo, we would produce the layout described below, in a local feed using the NuGet add api. The idea is the a single official build will produce a feed for packages, one for symbols and a folder for other build assets. The index.json is generated at publish time. It contains the registration url (which will be the container) which describes the feed where the packages live and the respective versions.

## Layout 

 - packages/
   - `*`.nupkg
 - assets/
   - `*`.`*` (installers, tar files, etc.)
   - `*`.symbols.nupkg
   
There are two scenarios that an individual repo will support. 

 * An official build:   
    - Input feed: https://dotnetcore.azurewebsites.com/packages OR MyGet 
    - Intermediate feed: Transport feed 
    - Output feed: https://dotnetcore.azurewebsites.com/packages OR MyGet 
   
   There is a single official feed which contains all the outputs of every component repo. The repo will use a feed where multiple versions of the product exist. You would be dependent on the versions which are decided by the Maestro updates, to determine what is going to be consumed. The publish step will need to update the versions index.json. There will be race conditions for parallel builds, this will be solved using the leasing lock on a blob (Azure Rest API) during a build.
   
   Every build will consist of multiple legs, these legs will need to publish to an intermediate container before Finalize. This container is the transport feed, it will contain the index.jsons that need to be merged with the final feeds' index.jsons.
   
   If we need to migrate external packages, older versions of packages as build dependencies, or some other restore dependency , the operation would just be pushing those nupkgs to the feed.
  
 * An orchestrated source-build:
    - Input feed: https://dotnetcore.azurewebsites.com/<build-SHA in Source build>/packages 
    - Intermediate feed: https://dotnetcore.azurewebsites.com/<build-SHA in Source build>/packages 
    - Output feed: https://dotnetcore.azurewebsites.com/<build-SHA in Source build>/packages 
   This is where there is a source-build orchestrated build definition which passes in a build number, which is used by all the legs of the definition which build a single repo. The transport feeds are used to carry the packages from the different legs. There shouldn't be an assumption that the build will happen completely locally on a machine, and so we need to support restoring from a transport feed. We use the commit SHA of the last update to source-build which tracks the last time a repo's submodule was updated. We don't push to the single official blob feed even if the build succeeds, as the packages produced will have similar versions. 
   
   Here there is no intermediate feed. The goal is that the final feed produced will be the feed that can be restored and tested from.
   
TODO: Do we require a separate subscription for all build output? 
**Casey concerns:**
> Depending on how many downloads you get a storage account could hit it's limits pretty darn quick.

For example, an official build of CoreFx which depends on CoreCLR - 
 * The official build of CoreCLR would complete
 * In an official build, the NuGet.config would point to https://dotnetcore.azurewebsites.com/packages as the only source for restore.
 * Each leg publishes package versions to https://dotnetcore.azurewebsites.com/corefx/master/12345.67/packages. They also create the index.jsons.
 * After every leg is completed, we finalize the build by leasing the final feed, pulling down the its index.jsons, merging them with jsons from the build. Then the final push to https://dotnetcore.azurewebsites.com/packages 
 
In a source-build,
 * Any external packages required for source-build would be restored from https://dotnetcore.azurewebsites.com/source-build/packages. This feed is maintained separately.
 * The CoreCLR build would finish and publish it's packages to a new feed, https://dotnetcore.azurewebsites.com/<build-SHA in Source build>/packages
 * CoreFX will configure it's NuGet.config to point to that feed as it's only source for restore.
 * It publishes it's package versions to https://dotnetcore.azurewebsites.com/<build-SHA in Source build>/packages, which it calculates from the last published package.
 * (Optional) you could finalize the build and push to the final feed.
 
In this method, there is no need to list your dependencies on other component repos explicitly in a props file, since all the packages live in a single place.
 
## Generating Transport Feeds
There will be a tool to generate a transport feed. The current plan is to have this be a separate consumable package whose source lives in the buildtools repo. The requirement we are going to set for using this in your build, is to have a dependency on building with msbuild and using NuGet for restore/publish. The idea is not to force a full buildtools toolset on the repo.

The tool should perform the following.
 * NuGet based add to Local feed to construct the layout (reusing code from Nuget.Client)
 * Generate a root index.json 
 * Update the versions index.json
 * Generate container in blob storage
 * Push to blob storage.
 
## Consuming Transport Feeds
This will be handle by NuGet restore and relies on the NuGet.config file to specify the sources, which is constructed from the ConfigureFeed task.

## Things we lose with this implementation
 * Gallery ( we can use a storage explorer )
 * Transactional logs
 * Versioned services that cache data and optimize reads