# Arcade Validation

We need to make sure changes done in the Arcade SDK as well as in the [core packages](https://github.com/dotnet/arcade/tree/master/Documentation/CorePackages) 
don't break any of the consuming repos or Arcade itself. 

## Arcade Validation Policy

With the goal of being more transparent, eliminating surprises, and minimizing disruptions, we now will **only** deploy Arcade and/or machine (image) updates when the following criteria is met:
- Each bell-weather repo (defined as runtime, aspnetcore, installer) must be building green so build problems won’t be compounded by new Arcade versions or images.
- Arcade updates has been tested with each bell-weather repo.

Exceptions:
- Servicing continues as is today, with possible conflicts arising with images which we’ll need to deal on a case by case basis.  (this should also go away w/ future plans)
- If an Arcade or machine queue update (includes VS) is needed to unblock any build, then obviously we’ll do that.  (but with targeted changes)

## The process

### Self-build

1. Build the latest changes locally using the specified settings
2. Using darc, update the dependencies based on the packages built in #1
3. Execute the "official build" (this will restore the packages built in #1)

You can try this [locally](https://github.com/dotnet/arcade/blob/master/eng/validate-sdk.cmd).

### '.NET Tools - Validation' channel

Before, all Arcade's builds were "tagged" with the ".NET Tools - Latest" channel and since all the 
repos get Arcade dependencies from this channel, any introduced bug would break consuming repos.

Now, Arcade's builds go to the ".NET Tools - Validation" channel.

### [Arcade-Validation Repository](https://github.com/dotnet/arcade-validation)

This repository contains the scenarios where we validate the last produced version of the SDK as a consumer repository.

#### Validation Process

1. On every Arcade build dependencies will be updated and auto-merged when all the checks pass
2. Arcade validation [official build](https://dnceng.visualstudio.com/internal/_build?definitionId=282) 
is triggered. This will validate the version which was just “pushed” by Arcade
3. The following process updates are only valid for the current development branch of Arcade and will not affect release or servicing branches. 
    1. Within the Arcade validation process, Arcade will also be tested with the "Last Known Good" build within the last three days (or the latest passing build if there haven't been any builds of the repository in the last three days) of the following repositories: `dotnet/runtime`, `dotnet/aspnetcore`, and `dotnet/installer`
    2. If any of the builds of these repositories fails, the Engineering Services team will investigate the source of the failure.
    3. If the source of the failure is due to an infrastructure failure in Arcade (e.g. dependency flow, publishing, machine images, et cetera), the team will correct the issue and publish a new version of Arcade to validate. 
    4. If the source of the failure is due to a toolset failure that was flowed to Arcade (e.g. Roslyn, Nuget, MSBuild, et cetera), the team will inform the repository owner of the potential breaking change. 
    5. When the team determines that the version of Arcade can be promoted (because all the builds were successful or any breaking changes are unrelated to infrastructure), we will promote the validated version of Arcade to ".NET Eng - Latest". Any repositories subscribed to that should receive the updated version as normal. 
    6. Missy Messa will primarily handle the promotion of Arcade (Matt Mitchell will be the back-up promoter).

#### Validation Scenarios

The following scenarios are a part of [Arcade Validation](https://github.com/dotnet/arcade-validation)'s build process.

**Build**

The code in the repo is built using the pushed in version of Arcade SDK and core packages. During this 
phase, packaging, signing and publishing are validated automatically.

**[Signing](https://github.com/dotnet/arcade-validation/tree/master/eng/validation/templates/signing)**

Test and real signing are validated by attempting to sign files [here](https://github.com/dotnet/arcade-validation/tree/master/src/Validation/Resources). 
Since we download all the files in the folder we can easily modify the amount and type of files we sign.

Details on how the Signing package works [here](https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/Signing.md).

**[Testing](https://github.com/dotnet/arcade-validation/tree/master/eng/validation/templates/testing) (Send jobs to Helix)**

We validate external (anonymous) and internal scenarios of sending jobs to Helix.

Details on how sending jobs to Helix works [here](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/SendingJobsToHelix.md).

**Publishing to BAR**

When the Build portion of the validation build completes we publish the produced package to BAR. This 
package won't be consumed by any repo but we want to make sure changes in the SDK did not affect it.

While this is good enough to validate basic scenarios we still have to make sure we validate changes 
in tier one repos. This will be done post preview 2 as specified in [this](https://github.com/dotnet/arcade/issues/111) epic.
