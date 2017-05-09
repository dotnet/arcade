# Implementation Details 1st iteration
The following document shows the initial two implementation approaches for the first iteration of the repro environment solution according to the requirements.

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

## 1st approach
"Snapshot" of the machine that contains the failure instance. Provision a DTL machine and restore the state so that the dev can poke in it.
More details to come according to the investigation we are going to do.

## 2nd approach
In essence, Jenkins will redo a job (without publishing or updating the PR), and then give the machine to the dev to poke around with. 
In order to do this, Jenkins jobs will have a way to signal that the dev wants a repo from that job. In the case of Pipeline jobs, it would use the CI SDK to flag the job.
For other types of jobs, it would add a parameter to the job.
Once Jenkins assigns a machine and gets the resources it will identify the workspace, compress it and upload it to an Azure Storage Account.
To provision the machine we'll use DTL. When the machine is ready, it will download and restore the selected workspace.
After this is done, we'lllet the Dev know how to access the machine.

## Tools/Systems that will probably help in the future
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
