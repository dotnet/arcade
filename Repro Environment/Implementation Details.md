# Implementation Details 1st iteration (POC)
The following document shows the initial implementation approach for the first iteration of the repro environment solution according to the requirements.

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
Jenkins will redo a job (without publishing or updating the PR). In order to do this, Jenkins jobs will have a way to signal that the dev wants a repro from that job. For the purposes of this iteration, the jobs are going to have an extra paramenter that when used, it will signal the job as a repro environment job.

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

# Takeaways

## Why not sequestering machines after failure
*Cost*
-	Without investment in automation to bucket what constitutes an interesting failure that needs to be looked at, we need to sequester all machines that have failures, regardless of type.
- Roughly, 19% of all jobs in Jenkins fail today for whatever reason. Over a month where we ran ~200k jobs, this constituted a total of about 38k failures. If we kept the machines around after failures for 12 hours…
38k * 12 = **456k** hours. This will cost quite a bit. We could implement logic to deallocate the machines after execution, which would reduce cost, but:
  - We will incur an additional penalty because we will need to new allocate machines more frequently (10-15 mins for a new Windows machine, 3-5 for Linux).
  - If machines are deallocated, we need to change around how we allocate workspaces for Jenkins jobs. Today we use the temp disks because they are fast and cheap (don’t touch the storage accounts), but it is unlikely that a re-allocated VM would get the same temp disk again. Changing to use disk storage for test execution will incur additional cost.
  - A deallocated machine is effectively a shut-down version of the VM.  This is not the “running state” any longer.

*Non-Azure assets will be stressed/overloaded*
- Today we use a variety of Mac/arm64 and other resources. We have far fewer resources available in these areas. Sequestering machines here will be a significant burden on the live pools.

## Developers involved in the POC:
- Have a way to always disable the Jenkins process before taking a snapshot
- Independent of the outcome of the steps to save the OS Disk and the workspace, always enable the Jenkins process before finishing a Job.
- The Jenkins machines are hosted under the Subscription `DDDotnetCIClients`. The application in charge of taking the snapshot needs to have read permission to the Subscription.
- The application/Azure functions created need to behave read and write access to a Storage Account under the same Subscription the Jenkins machines are.
- The application created needs to handle at least two scenarios for the snapshots: 
  - Managed Disks
  - Un-Managed Disks
- Installing new programs in the Jenkins machines is not a trivial process so we need to adapt to what we have available. We need:
  - Compress and decompress files. => So far, the Windows machines have 7z installed. We need to verify that we can use other compression methods in non-windows machines.
  - Upload the compressed file to Azure. => Windows machines have AZCopy installed. We need to figure out how to use the Azure CLI to upload files to Azure in non-windows machines.
  - Call a service in charge of the snapshot. => So far, the Windows machines have PowerShell. We need to figure a way to call the service on non-windows machines.
- The service in charge of creating a new VM from a snapshot needs to have access to the Subscription where the VHD and the workspace are stored.
- The Subscription where the Jenkins machines are should have enough public IPs or other mechanisms that the new created VMs could use for the users to connect to them.
- Moving a snapshot from one Subscription group o another is costly and could take more than 4 hours.

## Stakeholders:
-...

