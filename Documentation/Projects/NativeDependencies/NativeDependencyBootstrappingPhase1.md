# Native Dependency Bootstrapping One-Page Overview

## Scope

- Pre-CLI toolsets

- Xcopy deployable native dependencies

### Out of scope

- Non-Xcopy deployable dependencies

  - ie, docker, [Visual Studio](https://github.com/dotnet/arcade/issues/64), etc...

## Goals

- Cloning a repo and building deploys versioned native dependencies (within [scope](#scope)) which are available for use in the repo's build

## Plan Overview

- Create zips / tarballs of a couple of xcopy deployable native dependencies (like cmake) which are stored and publically accessible in Azure blob storage.

- Determine how repos will define a dependency version list for native dependencies which fits into the [dependency description](https://github.com/dotnet/arcade/pull/120/files) spec or modifies it where necessary

- Create [Powershell](https://docs.microsoft.com/en-us/powershell/scripting/setup/installing-windows-powershell?view=powershell-6) and bash scripts which are capable of understanding the dependency list and downloading / extracting versioned dependencies so that they are available in the repo from a well-known (or defined) location.

## Alternatives considered

- dependencies distributed as Nupkg's - Why not use nupkg's as the distribution mechanism?  Nupkg's have a couple of desirable attributes including being versioned, tfm / runtime awareness, well-known format.  There are a couple of downsides though.

  - All implementations of the dependency are packaged into a single unit.  ie, you can't just download the Windows dependency from the package unless you package it separately.  You would always bring down the Windows, Linux variants, and OSX implementations for a native dependency.

  - Native dependencies are not (yet) supported by CLI.  There is some work in progress by CLI team to support global tools with native dependencies (and also repo tools), but that work is not yet available and not expected until late in the .NET Core 2.2 timeframe.  We could create global tools with managed console apps that wrap native dependencies, but there is an investment there and it's throw-away work (ie, not something customers currently expect / do).

- X-Plat choco - beyond the scope of what's necessary for xcopy deployable native dependencies

- MSI's, debian packages, etc - beyond  the scope of what's necessary for xcopy deployable native dependencies