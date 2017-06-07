# Implementation Details 1st iteration (POC)
The following document shows the initial implementation approach for the first iteration of the repro environment solution according to the requirements.

# Requirements
- The dev experience should be "simple" (single click) and "fast" (Less than 15 minutes to create the environment)
- Ability to config repro capability (saving data for later machine creation on failture) to repro the failure
- Ability to request a job to repro (automatically creates the environment)
- Must work for Windows and Linux  (MAC and ARM post prototype)
- Needs to work for both Jenkins and Helix (Jenkins only for prototype)
- Solution is for devs who work at Microsoft only
- Checking out a machine comes with sufficient data and access to connect and configure 
- The machine should be delivered in a known state which can reasonably be setup to repro
- Prereqs are either installed, or verified to be installed
- A workable version of debugger and compilers installed (not necessarily needed for prototype)
- Exact hash is restored (GitHub is what's initially supported)
- Matching symbols - especially for when we find a way to NOT rebuild the product (not necessarily needed for prototype)
- Provide method to move files onto/off-of machine
- Be able to easily build the product, build the test, and then run the tests (not necessarily needed for prototype)
- Be able to easily run the failing tests and see the results (not necessarily needed for prototype)
- Be able to attach a debugger to the test (not necessarily needed for prototype)
- Support retention policy for the snapshot/workspace data
- Decommision once dev indicates they are complete with repro (could be rudamentary for prototype)
- Be able to reasonably produce a "report" of all outstanding environments and their age and dev (not necessarily needed for prototype)
- Ability to download workspace for local (or custom) repro (not necessarily needed for prototype)

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
- The application created needs to handle at least the following scenarios for the snapshots: 
  - Managed Disks
  - Un-Managed Disks
  - VM Scale Sets
- Installing new programs in the Jenkins machines is not a trivial process so we need to adapt to what we have available. We need:
  - Compress and decompress files. => So far, the Windows machines have 7z installed. We need to verify that we can use other compression methods, like p7zip, in non-windows machines.
  - Upload the compressed file to Azure. => Windows machines have AZCopy installed. We need to figure out how to use the Azure CLI to upload files to Azure in non-windows machines.
  - Call a service in charge of the snapshot. => So far, the Windows machines have PowerShell. We need to figure a way to call the service on non-windows machines. It could be cURL or wget.
- The service in charge of creating a new VM from a snapshot needs to have access to the Subscription where the VHD and the workspace are stored.
- To create a VM we need to manage user credentials so we the user that requested the machine can have access to it.
- The Subscription where the Jenkins machines are should have enough public IPs or other mechanisms that the new created VMs could use for the users to connect to them.
- Moving a snapshot from one Subscription group o another is costly and could take more than 4 hours.

## Stakeholders:
- As much as possible have the repro tool working for both Helix and Jenkins.
- Create a dotnet-bot command to trigger the reproBuild job execution for the comments of the PR.
- When creating the VM, is it neccesary to have specific user/password combination? can we use a generic one?
  - A proxy on the server side could also be an option. Kind of what the BuildLab used to do.
- If there is more input required from the user, ask everything in one place, for example, when re-building the failing Job.
- For the prototype to have more impact, the repro tool should include jobs running in Linux too.
- When there is a transiet error, make sure eveything that what was created is correctly deleted.
- Evaluate retention policies and the managment of the VMs. For example, what should a dev do when it is done with the VM?
- Once the dev connects to the rehydrated machine, put information in the Desktop about the machine, the folders, where to start and how to repro the issue.
- Make the process more automatized. For POC purposes is ok, but for real use it involves too many steps. 
  - Only one click to connect to the repro machine next to my failure in Jenkins. It is ok to wait around 5 minutes to get the machine.
- Consider the option to turn the ReproBuild option ON by default in specific Jobs that are known to run corner cases.
- Re-consider the Subscription we are currently using. There could be security and IPs limitation.
- To consider the project valuable, it should target Mac and ARM too.
- The resulting tool should be Jenkins/platform independent.
- Explore options to dump the environment in a script and then run the script in the new VM. (each repro should be in charge of enabling the dumps)
- Revisit other options where we don't need to save all the OS Disk as currently it takes 128GB.
  - The ability to take the workspace out of the Jenkins machine and use it in local dev machines is a good 1st step to make a repro.
  - Other option could be use the templates Jenkins uses for the machine and then download the workspace there.
