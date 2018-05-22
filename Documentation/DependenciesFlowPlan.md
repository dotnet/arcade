# Dependencies Flow Plan
The components needed in order to flow dependencies through different repositories are: Darc, Maestro++ and, using Darc, the Product Dependency Store.
For more information take a look at [version querying and updating](./VersionQueryingAndUpdating.md).

This document outlines the main stages to implement the work needed to accomplish the dependencies flow.
 First, we'll focus on [prototype](#Prototype) and once validated, we'll start adding funcionalities to each component.
In order for this plan to be done, the following requirements are needed:
 - Product repositories use Arcade to build.
 - Repositories use Darc for dependency management.
 - Builds for both CI and official builds happen in VSTS.

**Note:** Repositories = at least CoreClr, CoreFx, CoreSetup, Roslyn, and CLI.

# Prototype
The goal of the prototype is to validate the interaction proposed between Darc and Maestro++, as well as the boundaries of each.
We will showcase it by the end of S136.

## Conditions:
- Darc and Maestro++ live in Arcade.
- Maestro++ uses Darc to update Arcade dependencies.

## Scenario:
1. A file gets updated in Arcade, that needs to go to other branches in Arcade.
2. Maestro++ trigger happens.
3. Maestro++ uses Darc to check the current version of that package XX in Arcade
    Maestro++ calls `get -l --remote -r repoUri -b branch`
4. Maestro++ determines if there is a need to update the dependency.
5. Maestro++ calls Darc asking to update the version of package XX to vXY.
6. Darc creates a PR in Arcade.
7. Dev merges the PR.

## Assumptions:
- The trigger of Maestro++ for the Arcade repo will be scheduled.
- Maestro knows Arcade depends on package XX.
- PR created will have one of the developers as owner.
- The PR created by Darc will need to be merged by a developer.
- PRs won't be monitored by Maestro++.
- We'll use a mock for the Product Dependency Store.

# Adding functionalities - V1
Once we have gathered feedback from the prototype, we can start adding functionalities to create Dependencies Flow V1. Targeting 3 sprints (S139) of work that will include:

## Product Dependency Store
### Desing the model of the Product Dependency Store
The Product Dependency Store is going to be used by:
- Builds: As part of the post-build steps, once the assests produced during the build have been published, the build needs to tell the Product Dependency Store about what was produced and where it is located at.
- Darc: Query and updates the Product Dependency Store.
- Mission Control: Shows the information of the dependencies to users.

### Implement API to access the Product Dependency Store
For V1, the minimun APIs needed are:
- Add
- Get
- Update

## Darc
### Interaction with the Product Dependency Store
Darc needs to do get, add, and push using the API of the Product Dependency Store

### Add the ability to set the owner of the PR
By using GitHub tokens, be able to set the owner of the PR.

### Push to more than one repo
Implement the functionality to be able to push changes to more than one repository.
This means define and write logic to validate the `RepoFile.xml` and be able to do `darc push -r "E:\RepoFile.xml" -t <GitHub Token>`

## Maestro++
### Implement Channel subscription mechanism to Arcade
This involves:
- Evaluate if the current model (manage the subscriptions in a repository with a JSON file) is sufficient.
- Have the subscriptions in the Arcade repository.

### Implement the ability to set policies.
For triggers, PR and auto-merge management.

**auto-merge**
The minimum set of policies we need to enable this functionality are:
- Set the group of checks/validations that are needed in order to determine the PR is green and ready to merge. Note that each repo will have a different request on what goes into a group of checks, for example:
    - Green CI on GitHub/VSTS PR
    - No newer version updates have been merged
    - No non-maestro bot commits have been added to the PR
    - Would committing the PR results in an invalid dependency graph (e.g. aspnet's core-setup dependency is no longer matching CLI's core-setup dependency)
- Turn on/off the ability to auto-merge a PR that has as owner Maestro++ (in the form of a Bot, for example). => at this point, this is not enabled and it is by default on the Dev

**Note** that policies are for each repository.

## On board 1 repository to use Darc and Maestro++ for their official builds.
It involves:
- Define which repository.
- Add the repository to the Maestro++ subscriptions mechanism.
- Set Maestro++ policies for the repository.
- Builds send information to the Product Dependency Store using the API to report on the assests produced, the location, and other Build information. 

# Adding functionalities - V2
## Local work for Darc
Make sure a dev can use Darc locally to update the dependencies of the repo, and other repos too. Targeting 2 sprints (S141) of work that will include:

## Finish implementing Product Dependency Store APIs

## On board other repositories to use Darc and Maestro++ for their official builds.
It involves:
- Add the repositories to the Maestro++ subscriptions mechanism.
- Set Maestro++ policies for each repository.
- Builds send information to the Product Dependency Store using the API to report on the assests produced, the location, and other Build information.

# Adding functionalities - V3...
## Mirror builds using Maestro++
Currently, Maestro is in charge of this builds. Part of the work here is to define how those mirrors are going to happen in this new world and eventually stop relying on Maestro to build .NET CORE 3.0.

## Speculative version flow
More information [here]( https://github.com/dotnet/arcade/blob/master/Documentation/Maestro.md#speculative-version-flow).

## Speculative product build
More information [here](https://github.com/dotnet/arcade/blob/master/Documentation/Maestro.md#speculative-product-builds).
