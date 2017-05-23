# Implementation Details 1st iteration
The following document shows the initial implementation approaches for the first iteration of the repro environment solution according to the requirements.

# Requirements
- Dev must have Microsoft credentials that we can manage
- Common interface to "check out" a machine (VM or otherwise)
- Checking out a machine comes with sufficient data and access to connect and configure 
- The machine should be delivered in a known state which can reasonably be setup to repro
- Prereqs are either installed, or verified to be installed
- Debugger and compilers installed
- Exact hash is restored (GitHub is what's initially supported)
- Matching symbols (especially for when we find a way to NOT rebuild the product)
- Provide method to move files onto/off-of machine
- Be able to easily build the product, build the test, and then run the tests
- Be able to easily run the failing tests and see the results
- Be able to attach a debugger to the test
- Decommision once dev indicates they are complete with repro
- Be able to reasonably produce a "report" of all outstanding environments and their age and dev

# Implementation 
![Implementation](./implementation.PNG?raw=true)

## 1. Signal job to repro
Jenkins will redo a job (without publishing or updating the PR). In order to do this, Jenkins jobs will have a way to signal that the dev wants a repo from that job. For the purposes of this iteration, the jobs are going to have an extra paramenter that when used, it will signal the job as a repro environment job.

## 2. Save running environment
Jenkins machines are configured to use unmanaged disks. It uses OS Disk and Resource Disks. To capture the environment we are going to "Snapshot" the OS Disk, compress the workspace (located in the Resource Disk) and upload both information to an Azure Storage Account.

## 3. Notify user
Display in the Jenkins console a message to tell the user that the environment has been saved and what the next steps are to run the functionality to create a new VM.

## 4. Create VM with repro environment
Execute an Azure Function with specified parameters that will create a VM with the same OS Disk that was used by the original job and restore the workspace directory. 
In case this is not possible, the VM will contain information about how to connect and download the workspace from Azure.

The machine will also contain scripts/configuration/readme files that will help the developer use the new environment.

## 5. Notify user
Display in the function console a message about the machine created and how to connect to it.

## Progress
For information about the progress, please go to the following Issues:
- Issue [751](https://github.com/dotnet/core-eng/issues/751).
- Issue [750](https://github.com/dotnet/core-eng/issues/750).

# Tools/Systems that will probably help in the future
*DTL*
- VM image management happens here (would love to find a way to share with CI and VSTS)
- Special artifacts selectable
- https://microsoft.sharepoint.com/teams/DD_DDIT/DDITLabs/Pages/Azure-DTL.aspx

*Asset Explorer (AE)*

AE is a tool (originally from Office) which manages inventory in "pools", allowing for check out and check in.  It does not manage the machines directly, but is simply a database of sort to keep track what's available and who has what.  The good news is that it has a web API.

- Our pool is \STB\DevDiv\DotNet
- Web services url is http://aee/ws
- To install the AE client is http://aee/installae.  IE is needed to install the client.
- Contact: Dale Hirt in DDIT

## Misc reference material
- dotnet-ci jumping off point: https://github.com/dotnet/dotnet-ci
- buildtools groovy example: https://github.com/dotnet/buildtools/blob/master/netci.groovy 

# Notes
- Both implementation complement each other. For cases where the snapshot is not enough, we'll use the Jenkins approach.
- We need to implement a mapping between the different machine names/configurations of Helix, Jenkins and DTL so we could assing the correct one to the Dev.
- We are going to set a storage policy of 24h for both approaches.
