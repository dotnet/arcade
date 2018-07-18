# Arcade SDK

The primary goal of the SDK is to provide a set of tools which are shared across all participating repos. It is mandatory for [Tier 1 Repos](TierOneRepos.md) to
consume the SDK.

## Origin

The initial version of the SDK is a port from [RepoToolset](../../../../roslyn-tools/tree/master/src/RepoToolset) but will be changing as we add more packages and functionality to it.

## What shared tools should be part of the SDK and which should not?

### Required

If a tool provides functionality which is meant to be used by all of the [Tier 1 Repos](TierOneRepos.md) then we should add it to the SDK. 

### Optional

If the provided functionality will only work for a couple of repos then these won't be part of the SDK and will have to be manually referenced in a project.

## Levels

1. **Required** files/targets contained directly in SDK. These are required by all repos and the ammount should be minimal. These include:

* version.targets
* sign.proj
* toolset.proj

2. **Required** `PackageReference`s in the SDK. These are required by all repos and the ammount should be minimal but only the references are included, not the actual
files. Samples are:

* Signtool
* Microbuild

3. **Opt-in** `PackageReference`s that are added via a `props` file in a repo that the SDK imports if it is present. These `PackageReference`s are for all unique packages
needed in that repo. Included are:

* VSIX extension creation
* Unit tests

## Requirements

Each package in the SDK complies with the same set of requirements as all packages under Arcade. You can find more details about these requirements
[here](Overview.md#toolset-nuget-package-requirements).
