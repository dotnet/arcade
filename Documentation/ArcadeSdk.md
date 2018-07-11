# Arcade SDK

The primary goal of the SDK is to provide a set of tools which are shared across all participating repos. It is mandatory for tier 1 repos to consume the SDK.

## Origin

The initial version of the SDK is a port from RepoToolset but will be changing as we add more packages and functionality to it.

## What shared tools should be part of the SDK and which should not?

If a tool provides functionality which is meant to be used by all of the tier one repos then we should add it to the SDK. Samples of these are:

* Signing
* Publishing
* Helix telemetry and jobs
* Versioning

If the provided functionality will only work for a couple of repos then these will have to be manually referenced in a project. Samples include:

* VSIX extension creation
* Unit tests

The SDK supports the concept of "graduation" where if an optional package starts to be used more by more repos then it will be "promoted" to mandatory.

## Requirements

Each package in the SDK complies with the same set of requirements as all packages under Arcade. You can find more details about these requirements
[here](https://github.com/dotnet/arcade/blob/master/Documentation/Overview.md#toolset-nuget-package-requirements).
