# Automatic Test Retry

Automatic Test Retry allows you to retry **helix-based tests**. The retry occur at an assembly level and the number of retries is configurable.  

It's important to know that all the reruns happen on the same machine and that the machines are not cleaned between reruns, this has an important implication: **tests should be idempotent on the same machine.** 

Ex. if you are uploading to any fixed URLs, those need to check if that process was completed, and if copying files on the hard drive, it should be resilient to the same copy command having previously ran. 

## How to get on board
As a first step you should [configurate the test-configuration.json](https://github.com/dotnet/arcade/tree/main/src/Microsoft.DotNet.Helix/Sdk#test-retry) file. This file should be allocated at `eng/test-configuration.json` and it will be [automatically picked up](https://github.com/dotnet/arcade/blob/b4fd1cc3817e0e85213dcc219ff7f7252761659f/src/Microsoft.DotNet.Helix/Sdk/tools/Microsoft.DotNet.Helix.Sdk.MonoQueue.targets#L8), no further changes are needed.

There are a couple of rules that your test should meet:
1. Using Helix
1. Using Azure Reporter - if your tests are already reported on azure this means that is all good
1. Tests should be idempotent on the same machine

## How to know if a test was retried
There are a couple of ways in which you can confirm if a test was rerun, we are going to divide then in failed and succeeded tests:

### Test failed

When your test failed, and you want to verify if it was retried or want to see the errors of every retry you can do the following:

1. AzDO Artifacts tab: Navigate to your Tests tab, select the test that you want to verify and then select your artifacts tab

    ![](./Resources/AzureDevOpsArtifactsTab.png?raw=true)

    The Artifact tab now has the necessary context to know if a test was rerun, this will be easily recognizable by two things:
    * On the title you will see `multiple executions` message 
    * `Previous executions` will be listed

    ![](./Resources/ArtifactsTab.png?raw=true)

1. **Looking at the logs:** In there you will notice that you have 2 console.*.log files (one for each execution), furthermore in the run_client.log, will be some lines about the rerun:
 `Test configuration for test set indicates 'RERUN', re-executing workitem...`

### Test Succeeded 

When you want to know which test succeeded after a rerun you should go to the AzDO Tests tab of your build and filter the test that "Passed on rerun".

As can be appreciated in the image below:

![](./Resources/AzureDevOpsPassedOnRerun.png?raw=true)

