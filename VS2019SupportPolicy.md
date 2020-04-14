# Overview of VS2019 Policy 
Provide a reasonable mechanism to scout new minor versions of VS 2019 (both preview and release) that will minimize disruption to existing production queues

## Definition used to determine release 
The versioning formation used below is as follows <Major.Minor.Patch>. As an example for version 16.5.1, 16 is the major version, 5 is the minor and 1 is the patch version. 

## Policy for supporting VS 2019 – Released version
DNCEng will create a temporary queue (buildpool.windows.vs2019.scouting.open) with 5 machines (20 cores) for each minor release of Visual Studio that can use to validate any changes made to the product. 

This queue will be activated approximately one week after the [release](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes) of a minor update and will be available for two weeks. After approximately 2 weeks, we will update the existing queues (any helix queue containing *.vs2019) will be updated as part of a regularly scheduled Helix Machines release.

Patch updates will be made directly to the queues as required by corporate security or when requested by customers

## Policy for supporting VS 2019 – Public Preview Versions
DNCEng will create a temporary queue (buildpool.windows.vs2019.pre.scouting.open) with 5 machines (20 cores) for each public preview of Visual Studio that can use to validate changes. This queue will be activated approximately one week after the release of [public preview](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes-preview) and will be available for two weeks. 

After approximagely two weeks, we will update the existing queues (any helix queue containing *.pre) as part of a regularly scheduled Helix Machines release

## Policy for supporting VS 2019 – Private Preview Versions
There is currently no Engineering Services support for providing VS private preview queues.
