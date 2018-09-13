# Moving Official Builds from DevDiv to DncEng

## Guidance

These are the general steps for moving an official build from https://dev.azure.com/devdiv (DevDiv) to https://dev.azure.com/dnceng (DncEng).  This document is geared slightly towards .NET Core 3 component repos.  This document does not discuss converting builds from a task based definition to a YAML based definition.  YAML based builds are not a strict requirement for moving to DncEng though we should take note of repos which are not using YAML based builds and decide upon a timeline for moving to YAML (YAML based builds are a requirement for .NET Core 3 component repos).

1. Update the [agent pools](#agent-pools) used by your build.  Example: https://github.com/dotnet/dotnet-cli-archiver/pull/18/files

2. Create an internal code repository for your [source code](https://github.com/dotnet/arcade/blob/master/Documentation/VSTS/VSTSGuidance.md#source-code) in https://dev.azure.com/dnceng/internal.

3. Add your repo to the dnceng/internal mirror. Example: https://github.com/dotnet/versions/pull/361/files

4. Create a build definition following the documented [guidance](https://github.com/dotnet/arcade/blob/master/Documentation/VSTS/VSTSGuidance.md#build-definitions).

5. Request signing approval

   To do this: 

    a. Go to Preview Features and turn off "New builds hub" (the "Request signing approval" link does not work in the "New builds hub")

    b. Navigate to the build definition and select "Request signing approval" from "..."

      - M2's name: [Your M2's name]

      - M2's alias: [Your M2's alias]

      - Justification: .Net Core product repo

    c. Your M2 will be notificed of the request and will have to approve the request before the MicroBuild team will review the request enable signing of your build definition.

## Agent pools

The [hosted agent pools](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=vsts&tabs=yaml) which are available in DevDiv are also available in DncEng.  If your build is using a hosted pool, or it is possible to use a hosted pool, it is recommended that you do so (for non-Windows builds).  The general reason that a hosted pool does not work for teams for Linux / Mac builds, is the current [10 GB capability](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=vsts&tabs=yaml#capabilities-and-limitations) on these machines which is not sufficient for most teams.  Azure DevOps is working to increase the disk size.

### Windows builds

Agent pool: DotNetCore-Windows

There are currently 5 machines in the DotNetCore-Windows agent pool and they are all signing capable.  We are in the midst of bringing up a different agent pool which will have far more machines that are all signing capable.

### Linux builds

Hosted agent pool: Hosted Ubuntu 1604

Non-Hosted agent pool: DotNetCore-Linux

Both the hosted and non-hosted agent pools have Linux machines with docker installed.

### Mac builds

Hosted agent pool: Hosted macOS

Non-Hosted agent pool: DotNetCore-Mac