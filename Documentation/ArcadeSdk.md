# Arcade SDK

The primary goal of the SDK is to provide a set of tools which are shared across all participating repos and actually, in order for a repo to be fully onboarded to Arcade
it has to consume the SDK.

## Origin

The initial version of the SDK is a port from RepoToolset but will be changing as we add more packages and functionality to it.

## What shared tools should be part of the SDK and which should not?

If a tool provides functionality which is meant to be used by all of the tier one repos then we should consider adding it to the SDK. If this functionality will only work
for a couple of repos then we could still include it as optional or leave it out of the SDK.

// TODO: close on what is the bar in order for a package to be included (or not) to the SDK

## Levels

The SDK has 4 different levels of packages depending on how mandatory these are:

### Level 1

Packages which are always included when consuming the SDK. Functionality of these include:

* Signing
* Publishing
* Helix related
* Versioning

// TODO: add missing. Also I know there has been some talks about making Versioning non-mandatory

### Level 2

Packages which are referenced or represent a dependency from packages in Level 1. These include:

* Newtonsoft.Json
* NuGet.Packaging
* WindowsAzure.Storage
* etc.
 
### Level 3

Optional packages.

// TODO: provide details on how would this be done in the SDK. i.e. by adding a reference to a file which if it's there then the packages will be included if not nothing
happens?

### Level 4

Packages defined in each repo.

// TODO: provide details on how would this be done in the SDK.

The SDK supports the concept of "graduation" where if a package in a lower level starts to be used more by more repos then it will be moved to lower levels making it more mandatory with each move.

## Requirements

Each package in the SDK is and will be governed by the same set of requirements all packages under Arcade currently are. You can find more details about these requirements
[here](https://github.com/dotnet/arcade/blob/master/Documentation/Overview.md#toolset-nuget-package-requirements).
