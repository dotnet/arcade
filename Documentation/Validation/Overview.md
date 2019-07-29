# Arcade Validation

We need to make sure changes done in the Arcade SDK as well as in the [core packages](https://github.com/dotnet/arcade/tree/master/Documentation/CorePackages) 
don't break none of the consuming repos nor Arcade itself. 

Before this effort, we'd know something was broken until we manually updated a dependency making 
things hard to track since the bug could have been introduced way in the past and could be already 
buried in a lot of commits.

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

### [Arcade-validation repo](https://github.com/dotnet/arcade-validation)

This repo contains the scenarios where we validate the last produced version of the SDK as a consumer repo.

#### Validation process

1. On every Arcade build dependencies will be updated and auto-merged when all the checks pass
2. Arcade validation [official build](https://dnceng.visualstudio.com/internal/_build?definitionId=282) 
is triggered. This will validate the version which was just “pushed” by Arcade
3. If all scenarios succeed, we move the build to “.NET Tools – Latest”
4. Since consuming repos are still getting changes from “.NET Tools – Latest” channel, they get the latest 
validated versions with no change on their side

#### Validation Scenarios

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
