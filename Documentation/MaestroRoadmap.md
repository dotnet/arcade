# Maestro++ Roadmap
In order to minimize disruption in the official builds with the implementation of Maestro++, Maestro++ is going to be developed in stages where it will depend on some functionalities of Maestro.
 Once we start adding new functionalities to Maestro++ we'll remove the dependency in Maestro until it is no longer needed.

 For this to happen, the following requirements are needed:
 - Product repositories use Arcade to build.
 - Repositories use Darc for dependency management.
 - Build for both CI and official builds happen in VSTS.

This document outlines the main stages to implement Maestro++ according to the [requirements](https://github.com/dotnet/arcade/blob/master/Documentation/Maestro.md).
 First, will be to create a [prototype](#Prototype) and once validated, start [adding funcionalities](#Adding-functionalities) to it.

# Prototype
The main focus of the prototype is to make sure that Maestro++ uses Darc to update the dependencies during official builds.

This means:
- Maestro++ lives in Arcade.
- Maestro++ will use the channel subscription mechanism defined in the dotnet/versions repository in order to know what to update.
- Maestro++ will keep using the dotnet/versions repository to know when packages have been updated.

# Adding functionalities
Once we have gathered feedback from the prototype, we can start adding functionalities to Maestro++ and therefore, start removing functionality from old Maestro.

## Policies to manage auto-merge
The minimum policies we need to enable this functionality are:
- Set the group of checks/validations that are needed in order to determine the PR is green and ready to merge. Note that each repo will have a different request on what goes into a group of checks, for example:
    - Green CI on GitHub/VSTS PR
    - No newer version updates have been merged
    - No non-maestro bot commits have been added to the PR
    - Would committing the PR results in an invalid dependency graph (e.g. aspnet's core-setup dependency is no longer matching CLI's core-setup dependency)
- Turn on/off the ability to auto-merge a PR that has as owner Maestro++ (in the form of a Bot, for example).

**Note** that policies are for each repository.

## Implement Channel subscription mechanism to Arcade
This involves:
- Evaluate if the current model (manage the subscriptions in a repository with a JSON file) is sufficient.
- Maestro++ should start using this new mechanism, instead of the one managed by old Maestro.

## Package publish trigger
Currently, Maestro knows that dependencies need to flow because the dotnet/versions repo gets updated. In order to change this behavior, we need at least two things to happen:
1. Product repositories use Arcade for their builds so telemetry is sent at different stages of the build. One of these events, will tell Maestro++
that packages got published and are ready for consumption, which will trigger the dependency flow.
2. Product repositories are using Darc for their dependency management.

The work here is:
- Remove the dependency of dotnet/versions repository in Maestro++ and instead use the telemetry sent by the build.
- Define how the telemetry sent by the build is going to generate a response from Maestro++ (how, where, etc)

## Mirror builds
Currently, Maestro is in charge of this builds. Part of the work here is to define how those mirros are going to happen in this new world and eventually stop relying on Maestro to build .NET CORE 3.0.

## Speculative version flow
More information [here]( https://github.com/dotnet/arcade/blob/master/Documentation/Maestro.md#speculative-version-flow).

## Speculative product build
More information [here](https://github.com/dotnet/arcade/blob/master/Documentation/Maestro.md#speculative-product-builds).
