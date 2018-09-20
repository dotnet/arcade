# Native Toolset Bootstrapping Design

This document is intended to provide insight into the design of native toolset bootstrapping / installation mechanisms.  This document will guide the implementation of native toolset bootstrapping.

## Overview

Arcade will provide a set of common installation libraries which will be provided to partipating repos via Maestro.  The common libraries will be used to acquire "installers" for native components.

Repo's will be provided a local bootstrapping file (both an ps1 and an sh file).  The bootstrapper uses the common libraries to install native toolset dependencies.

## Definitions

Tool - a native toolset dependency (cmake, python, etc...)

Native Asset - Packaged native artifact, also known as a tool but specifically related to the asset as provided by the publisher of the tool.  ie, in the case of cmake, one of the zip or tar files from https://cmake.org/download/

Installer - a script(s) used to deploy a native asset

Shim - wrapper script which is deployed to a platform that is referenced to execute the provided tool

Common Library - set of libraries available for native asset deployment to a platform

## Arcade toolset libraries

Arcade will provide a set of libraries which will be used to install native tools.  These libraries will include [entry point scripts](#entry-point-scripts), the [common libraries](#common-library-scripts), and [tool-specific install scripts](#native-tool-installers). **The Arcade toolset libraries will be distributed via Maestro updates.**

### Entry-point scripts

The entry-point scripts are the scripts which repos will use to bootstrap their defined native toolset dependencies.  The entry-point scripts will read the repo's `global.json` file to determine which tool(s) and version to install.  Only one version of each tool should be defined, though there is not (yet) logic to detect multiple tool versions being installed (currently if this occurs, last one installed will win).

Entry-point scripts are:

- init-tools-native.cmd

- init-tools-native.sh

These scripts will also be wired into the `eng\common\Build.ps1` and `eng\common\build.sh` scripts so that they will run as part of a repo's restore operations.

### Common libraries

The common libraries will be used to determine which individual [native tool installers](#native-tool-installers) are required by the repo and will execute the installers.  They will be written in commonly supported formats (ps1 or bash).

### Common library scripts

Development will show which common libraries will actually be required, but some examples of common library tasks include the following:

- Parse dependency requirements
- Install Xcopy Toolset
- Generate shims
- Download a file
- Extract an archive
- Determine current architecture
- Determine current OS

### Native tool installers

The Arcade repo will define the installers for each supported native tool.  The native tool installer will define how to install a tool locally (from blob storage).  Certain, common install scenarios (xcopy deployable) may use a common library to perform the install.  The install scripts will (initially) be generic install scripts (per tool) used to install any version of the native asset which has been published to Azure storage.  If tool install formats noticeably change from version to version, we may need to adjust the install scripts accordingly (while maintaining backward compatibility).

Native tool installers will be responsible for supporting an "install" operation and a "clean" (uninstall) operation.  See existing installers for examples of "clean". 

#### Shims

Most [native tools](#native-tools-installers) will need to provide a "shim" via the common library scripts.  "Shims" are used to execute the native toolset.  Since there is no enforced structure on the file layout for a native asset, shims bridge the gap so that we have a single well-known entry point that can be used for our native toolset.  Having shims allows us to put all of them in a single folder (in a given repo) so that we can use them to access the tools rather than managing path access to every known toolset.

It is possible that a native toolset will require more than one shim.

## Azure Blob Storage Format

Native toolset assets will be placed in an Azure blob storage container.  The default location is https://netcorenativeassets.blob.core.windows.net/resource-packages

## Blob storage layout

```Text
\external
  \any (resources that would work for any OS)
  \linux
    \tool1
      -installer
      -additional resources needed for install
    \tool2
  \windows
    \tool1
    \tool2
```

### external resources folder structure

The `external` folder is a folder structure that contains all installers and resources for external depencencies.  These are zips / tarballs /etc... provided by a tool publisher which we have republished into Azure blob storage, organized in folders by the operating system and tool to be installed.

In cases where the resource's version is not identifiable by the resource's filename, the resource must be uploaded into a folder that allows the installer to disambiguate the version.

## Example - resource-packages container

```Text
\external
  \linux
    \cmake
      -cmake-3.11.1-Linux-x86_64.tar.gz
      -cmake-3.11.0-Linux-x86_64.tar.gz
  \windows
    \cmake
      -cmake-3.11.1-win32-x86.zip
      -cmake-3.11.1-win64-x64.zip
    \python
      -python-3.6.5-embed-amd64.zip
      -python-3.7.0b3-embed-win32.zip
    \vcredist
      \14.0
        -vc_redist.x64.exe
        -vc_redist.x86.exe
      \15.0
        -vc_redist.x64.exe
        -vc_redist.x86.exe
```

## Questions

**How will we handle installers if there are distro specific requirements?**

This will likely come up very quickly and deserves consideration.  The current plan is to allow each installer to handle this as needed.

**I need to onboard a new native tool. What do I do?**

* Upload the required files for tool installation to the https://netcorenativeassets.blob.core.windows.net/resource-packages azure storage container
* Write an installer for the tool. These should be scripts called 'install-tool.ps1/sh'
    * ps1 example: [install-cmake.ps1](https://github.com/dotnet/arcade/tree/master/eng/common/native/install-cmake.ps1)
    * sh example: [install-cmake.sh](https://github.com/dotnet/arcade/tree/master/eng/common/native/install-cmake.sh)

**How do you determine which version of the common libraries / installers to use?**

Maestro will provide updates to the scripts.

**How do you determine which native tools to install?**

The native tools will be defined in a `native-tools` section of the repo's `global.json` file.

Example:

```JSON
{
    "sdk": {
        "version": "2.1.100-preview-007366"
    },
    "msbuild-sdks": {
        "RoslynTools.RepoToolset": "1.0.0-beta2-62719-04"
    },
    "native-tools": {
        "cmake": "3.11.1"
    }
}
```

**Why is each native dependency required to have an "installer", why isn't the local repo handling unzipping and laying out the assets?**

I think that this model will allow us to be a bit more flexible in the types of dependencies that we install and provide a method for non-xcopy deployable dependencies to be installed in the future.  The tool installers may make use of common libraries for installs though.
