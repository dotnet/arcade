# Signing plan

## Goals

- Design a build system which supports our goal of a two hour build time

- Minimize the differences between offical and PR builds, thus increasing the reliability of official builds

- Minimize the impact of unreliable systems such as signing

## Overview

Signing is a requirement for artifacts that are officially available for our end customers. It is not required for artifacts that exist solely as an intermediate stage in our official build. Signing is also not required immediately on artifacts which will eventually be available to customers but instead merely needs to be done before we advertise / push the artifact to a channel that we communicate to customers as a supported channel.

Removing signing as an intermediate step is both important for the throughput of our builds as well as reliability. Presently the time variance of signing for dotnet/runtime is over one and a half hours. That means a normal variance in our signing process guarantees the two hour build goal will not be met. Giving us flexibility in how signing occurs, and mostly making it an asynchronous process, is required for us to be successful.  

Going forward the expectation is that most repositories will remove signing from their official builds. This will be a hard requirement for any repository on the critical build path of .NET. For other repositories it will be strongly recommended but not required.  The core-sdk repository will be responsible for producing all of the signed artifacts for the .NET product.

## Signing in repositories

The responsibility of individual repository builds is to produce artifacts that either ship to customers (shipping repository) or are consumed by other repositories for their own builds (dependency flow repositories).

### Dependency flow repositories

- will not real sign

- will disable signing validation (if they have fully stopped producing signed assemblies)

- will only publish to Azure Artifacts

- will publish to a different (unsigned) artifact feed

- will change their subscriptions to consume from the unsigned feed

- will not need / use an `eng/Signing.props` file

### Shipping repositories

- will contain the eng/Signing.props file used to configure signing

- *will real sign shipping packages, MSI's, debs, rpm's, etc... by extracting the contents, signing binaries, and re-packaging.

- will publish to signed and unsigned artifact feeds.

\* *Initial investigation and exploration has shown that it **should** be possible to extract an MSI's contents, sign, and repackage them.  The current plan is contingent upon being able to repackage installers.  If that proves not to be possible, then we will scrap this plan and pursue other options (such as a queue/await model) to reduce sign times during official builds.*

## Moving in stages

Stage 1:

- Switch repos to consume / publish to unsigned feed and publish to signed feed

Stage 2:

- Move MSI signing to core-sdk

While, currently, there are less blockers to moving package signing to core-sdk than moving MSI signing, this entire plan is dependent on the ability to repackage / sign MSI's in core-sdk, so moving packages first may result in throw away work.

Stage 3:

- Move package signing out of dependency flow repositories and into core-sdk

- Disable publish to signed feeds

- Disable signing validation from dependency flow repositories if it is no longer needed

Stage 4:

- Move RPM / deb / etc signing into core-sdk

- Disable signing validation from dependency flow repositories

## Notes

- The ability to repack MSI's is the biggest risk to this plan.  If this does not prove to be possible, then we will provide another plan which adjusts accordingly.

- TBD: With respect to signing validation, what does this mean for repos which are producing more than just nupkg's but aren't signing nupkgs?  

  - Do we need to modify signing validation to support validating some (not all) artifacts?

  - Do we just modify people's builds to publish signed artifacts to one Azure DevOps build artifact and unsigned to another?

- Investigation into signing MSI's is underway

- We have not begun exploring signing RPM's, Deb's, etc... yet but it is not expected that is a high risk part of the plan.
