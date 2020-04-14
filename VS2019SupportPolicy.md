# Overview of VS2019 Policy 
Provide a reasonable mechanism to scout new minor versions of VS 2019 (both preview and release) that will minimize disruption to existing production queues

## Policy for supporting VS 2019 – Released version
DNCEng will create a temporary queue (buildpool.windows.vs2019.scouting.open) for any minor release of Visual Studio that can use to validate any changes made to the product. 

This queue will be activated approximately one week after the [release](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes) of a minor update and will be available for two weeks. After approximately 2 weeks, we will update the existing queues (any helix queue containing *.vs2019) will be updated as part of a regularly scheduled Helix Machines release.

Patch updates will be made directly to the queues as required by corporate security or when requested by customers

## Policy for supporting VS 2019 – Public Preview Versions
DNCEng will create a temporary queue (buildpool.windows.vs2019.pre.scouting.open) for the public preview release of Visual Studio that can use to validate changes. his queue will be activated approximately one week after the release of [public preview](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview) and will be available for two weeks. 

After approximagely two weeks, we will update the existing queues (any helix queue containing *.pre) as part of a regularly scheduled Helix Machines release

## Policy for supporting VS 2019 – Private Preview Versions
There is currently no Engineering Services support for providing VS private preview queues.
