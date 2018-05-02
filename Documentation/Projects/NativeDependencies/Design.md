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

The entry-point scripts are the scripts which repos will use to bootstrap their defined native toolset dependencies.  The entry-point scripts will read the repo's `NativeToolsVersions.txt` file to determine which tool(s) and version to install.  Only one version of each tool should be defined, though there is not (yet) logic to detect multiple tool versions being installed (currently if this occurs, last one installed will win).

Entry-point scripts are:

- nativetoolsbootstrap.cmd

- nativetoolsbootstrap.sh

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

The Arcade repo will define the installers for each supported native tool.  The native tool installer will define how to install a tool locally (from blob storage).  Certain, common install scenarios (xcopy deployable) may use a common library to perform the install.  The install scripts will (initially) be generic install scripts (per tool) used to install any version of the native asset which has been published to Azure storage.  If tool install formats noticeably change from version to version, we may need to adjust the install scripts accordingly (while maintaining backward compatability).

#### shims

Most [native tools](#native-tools-installers) will need to provide a "shim" via the common library scripts.  "Shims" are used to execute the native toolset.  Since there is no enforced structure on the file layout for a native asset, shims bridge the gap so that we have a single well-known entry point that can be used for our native toolset.  Having shims allows us to put all of them in a single folder (in a given repo) so that we can use them to access the tools rather than managing path access to every known toolset.

It is possible that a native toolset will require more than one shim.

## Azure Blob Storage Format

Native toolset assets will be placed in an Azure blob storage flat file structure.

## Blob storage layout

```Text
\nativeassets
  - [flat file drop of zips, tarballs, etc...]
```

### nativeassets

The `nativeassets` folder is a flat file dump of all dependencies.  This is assuming that the tools are distributed with versioned filenames.  These are zips / tarballs / etc... provided by a tool publisher which we have republished into Azure blob storage. It is the current hope, that we do not have to repackage the assets to include additional metadata that would make them different from the originally published asset.

## Example - Azure blob storage container

```Text
\nativeassets
  -cmake-3.10.3-win32-x86.zip
  -cmake-3.10.3-win64-x64.zip
  -cmake-3.11.1-win32-x86.zip
  -cmake-3.11.1-win64-x64.zip
  -python-3.6.5-embed-amd64.zip
  -python-3.7.0b3-embed-win32.zip
\installers
  \1.0.0-preview1.08530.0+asdf34234
    -commonlibrary-1.0.0-preview1.08530.0+asdf34234.zip
    -commonlibrary-1.0.0-preview1.08530.0+asdf34234.sh
    -install-cmake-3.10.3.ps1
    -install-cmake-3.10.3.sh
```

## Questions

**How will we handle installers if there are distro specific requirements?**

This will likely come up very quickly and deserves consideration.  The current plan is to allow each installer to handle this as needed.

**How do you determine which version of the common libraries / installers to use?**

Maestro will provide updates to the scripts.

**How do you determine which native tools to install?**

The current format is a parsable text file

Example:

```Text
CMake=3.11.1
Python=3.6.5
```

**Why is each native dependency required to have an "installer", why isn't the local repo handling unzipping and laying out the assets?**

I think that this model will allow us to be a bit more flexible in the types of dependencies that we install and provide a method for non-xcopy deployable dependencies to be installed in the future.  The tool installers may make use of common libraries for installs though.
