# CMake Scenarios

This document presents the scenarios for CMake in .NET Core. Each subheading below is a name of a scenario. Under each subheading there will be a short description of the scenario and a narrative. The narrative includes an actor, flow of events, and the resulting outcomes. An outcome consists of one or more pairs of the current and desired experiences.

 * [Build a .NET Core repository on a clean machine](#build-a-net-core-repository-on-a-clean-machine)
 * [Setup an official build for a .NET Core repository](#setup-an-official-build-for-a-net-core-repository)

## Build a .NET Core repository on a clean machine
A .NET Core user (Microsoft employee, a contributor or someone in the open) clones a .NET Core repository and attempts to build.

### Narrative
I am an IT Manager from Contoso who was inspired by .NET Core demoes at [Connect(); 2016](https://msdn.microsoft.com/en-us/magazine/connect16mag.aspx), and I would like to contribute to .NET Core. 

Flow of events:
 1. I setup a clean Windows 10 VM through my Azure subscription.
 2. I clone CoreFx repository from [dotnet/corefx](https://github.com/dotnet/corefx.git)
 3. I attempt to build the repository using the build command (build.cmd)

### Outcome #1 Build fails
#### Current
Build fails saying CMake, which is a prerequisite is missing on the VM.  Though the error message provides an URL from where CMake can be downloaded from, it does not list a specific version or a range of supported versions. I am not certain on what version to download.

#### Desired
Either:
 * Error message informs me the specific version of CMake to download, and the suggested source to download it from. I download the specific version and perform a default install. *(TBD: Should the user restart Command or Terminal window?).* Running build command now succeeds.
 * Error message informs the me the specific version of CMake to download, and the suggested source to download it from OR to perform a gesture to acquire CMake. I perform the gesture so that a tool downloads and extracts the declared version of CMake to a .NET Core sandbox tools folder. Running build command now succeeds.

## Build a .NET Core repository on an existing development machine
A .NET Core user would like to clone a .NET Core repository to his/her existing development machine, and build that repository.

### Narrative
I am an IT Manager from Contoso who was inspired by .NET Core demoes at [Connect(); 2016](https://msdn.microsoft.com/en-us/magazine/connect16mag.aspx), and I would like to contribute to .NET Core. 

Flow of events:
 1. On my existing development machine, I cloned CoreFx repository from [dotnet/corefx](https://github.com/dotnet/corefx.git)
 2. I attempted to build the repository using the build command (build.cmd)

### Outcome #1: Build succeeds
**Current**: I have no indication about the CMake version used.
**Desired**: I can refer to a build artifact to find the version of CMake used in the build. I get a build warning if the version used in the build is not the declared version.

### Outcome #2: Build fails since CMake version available on the machine is the declared version
**Current**: Build potentially fails in strange ways.
**Desired**: Build fails with an error message informing me that a CMake version was detected, and the version is not the declared version. I install the declared version of CMake or perform a gesture so that a tool downloads CMake to a .NET Core sandbox tools folder. Thus, CMake is available for the build.

### Outcome #3: Build fails saying CMake, which is a prerequisite is not available on the machine
**Current**: I try to download CMake based on the error message. I am not certain on what version to download.
**Desired**: Same as the desired experience in earlier scenario (Build a .NET Core repository on a clean machine.)

## Setup an official build for a .NET Core repository
A .NET Core repository owner would like to setup a reliable, repeatable and trustable process of producing official builds.

### Narrative
I am the owner of [.NET CoreFx](https://github.com/dotnet/corefx) repository.  I would like to setup a process that will produce reliable, repeatable and trustable builds for this repository. 

Flow of events:
 1. Create a new VSTS build definition that runs the build command of the repository
 2. Ensure the builds succeeds

### Outcome #1: Official builds consume the declared version of CMake.
**Current**: .NET Core repository is built using a version of CMake installed while setting up the VM. Version of CMake is not logged in build artifact. 

**Desired**:
 1. I can find the declared version of CMake for CoreFx or any .NET Core repository.
 2. I can setup official build of a .NET Core repository such that the build acquires the declared version of CMake from OSS Tools repository, and places it in a sandbox location.
 3. I can ensure that the official build utilizes CMake tool from the sandbox folder.

Note: OSS Tool repository will download CMake binary, security audit the source, build and then host. Thus, minimizing any security risks that might arise when CMake is consumed directly from internet.

### Outcome #2: Official builds setup and maintenance is reliable and costs lowered
**Current**: I find that the build agents are tied to a specific version of CMake.
**Desired**: Build VMs are more contained and not dependent on a particular branch of .NET Core. Thus, build from multiple repositories can happen on a same VM.

## Service a .NET Core repository
A .NET Core user would like to service a release branch and needs the release configuration to rebuild.

### Narrative
I am a .NET Core team member who is assigned to service a [CoreFx](https://github.com/dotnet/corefx) release.  I checked out the release branch on my local developer machine. I investigated the issue reported in that particular branch, and have a fix ready for test. 

Flow of events:
1. I create a service branch i.e., fork from release branch
2. I follow the developer guidelines available for that branch to perform a build 

### Outcome #1: Service branch builds consume the declared version of CMake
**Current**: Since a declared version of CMake is not available in the repository, I will build the service branch using the latest version of CMake available. This latest version of CMake might introduce new product behaviors that I will have to resolve. Thus, lack of declared version introduces uncertainty and additional costs in servicing a branch.
**Desired**:
 1. I can run the build such that if the required toolset does not match then, build fails.
 2. I can find out the CMake version used to build the release branch.
 3. I can acquire and run build with tools in a sandbox folder.

## Revise the CMake version of a .NET Core repository
A .NET Core team member can update the declared CMake version for a given .NET Core repository.

### Narrative
I am a .NET Core team member working on new features in CoreFx. These features require the latest version of CMake.

Flow of events:
 1. I verified the compatibility of existing features with the new version of CMake
 2. I would like to update official builds to consume this new version of CMake.

### Outcome #1: All build scenarios are aware of the new version of CMake
**Current**: Though as a .NET Core repository user I can build a local repository using different versions of CMake, doing the same with official builds involves cost such as updating build definition, notifying repository owners, and finally inform the open community of users about the new version of CMake.
**Desired**: As a .NET Core team member I can modify declared CMake version, and submit this change as a pull request (PR). The change to declared CMake version is reflected in all build scenarios. This means, the official builds will pick up the new version from OSS Tools repository, and .NET Core users can refer to documentation that lists the new declared version of CMake.

## A new version of CMake can be tested against a .NET Core repository
A .NET Core team member can try a new version of CMake to build a .NET Core repository.

### Narrative
As a team member of .NET Core or Engineering Services, I would like to check the applicability of a new version of CMake in a .NET Core repository. For instance, verify if a new version of CMake is compatible with a .NET Core repository, and no unexpected regressions in the product are introduced.

Flow of events:
 1. I build the local repository using the new version of CMake. Ensure build succeeds
 2. I host the CMake package is OSS Tools Repository.

### Outcome #1: A test run of official build  consuming the new CMake package can be performed
**Current**: Same as the current experience in earlier scenario (Revise the CMake version of a .NET Core repository.)
**Desired**:
 1. I'm able to build a .NET Core repo using a CMake version, which is not in the declared version.
 2. I can modify the prerequisite step in official build so that it acquires my CMake package from OSS Tools repository.
 3. I can perform a test run of official build and ensure that the CMake package is consumed, and build succeeded.

## Guidance for setting up official builds for a .NET Core repository is available
A .NET Core repository owner refers to documentation that describes the procedure to setup up official builds.

### Narrative
I'm team member on Red Hat.  I would like to setup official builds of our CoreFx repository using the same CMake version used in .NET Core official builds.

Flow of events:
 1. I clone CoreFx repository from [dotnet/corefx](https://github.com/dotnet/corefx.git)
 2. I follow the developer guidelines to setup official builds

### Outcome #1: 
**Current**: I will use the latest version of CMake in official builds since CoreFx documentation does not provide any guidance on declared CMake version. I submit a PR to CoreFx. PR fails CI. After spending lot of time on investigating the failure it is realized that official builds of a .NET Core repository at Red Hat is using a different version of CMake than that used by .NET Core product team.
**Desired**: 
 1. Declared version of the .NET Core repo is in the open.  I can find it, and enforce it with my toolset story.
 2. I can specify a version of CMake that should be consumed in the build command of my .NET Core repo.
