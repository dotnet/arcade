#  Prototype Implementation Details
The following document shows the implementation details for the prototype of the repro environment solution according to the requirements.

# Requirements
- The dev experience should be "simple" (single click) and "fast" (Less than 15 minutes to create the environment).
- Ability to config repro capability (saving data for later machine creation on failure) to repro the failure.
- Ability to request a job to repro (automatically creates the environment).
- Must work for Windows and Linux.
- Needs to work for Jenkins.
- The solution is for devs who work at Microsoft only.
- Checking out a machine comes with sufficient data and access to connect and configure.
- The machine should be delivered in a known state which can reasonably be setup to repro.
- Prereqs are either installed or verified to be installed.
- Exact hash is restored (GitHub is what's initially supported).
- Provide a method to move files onto/off-of machine.
- Support retention policy for the snapshot/workspace data.
- Decommission once dev indicates they are complete with repro (rudimentary).
- Start gathering telemetry of the system, like i.e. how many times it is being used, and by which repo. 

# Nice to Have
- A workable version of debugger and compilers installed.
- Matching symbols - especially for when we find a way to NOT rebuild the product.
- Be able to easily build the product, build the test, and then run the tests.
- Be able to easily run the failing tests and see the results.
- Be able to attach a debugger to the test.
- Be able to reasonably produce a "report" of all outstanding environments and their age and dev.
- Ability to download workspace for local (or custom) repro.

# Dev workflow

## Request a repro
There are two flows that could trigger the request of a repro: have a Jenkins Job that when failed it automatically will [save the running environment](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#2-save-running-environment) so later the dev can request a [VM with the repro environment](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#4-create-vm-with-repro-environment) or rebuild a Job and signal it to automatically save the running environment and create a VM as part of the Job.

### 1. Save running environment by default
![](./Images/Scenario1.png?raw=true)

The repro owners have the ability to request this feature per Job creation so that not all the jobs have an automatic saving option.
1) A job fails and the running environment is saved. The log of the job will inform the dev about the process in the Jenkins console log.
2) The dev logs in in Jenkins.
3) A link will tell the user about the option of creating a VM with the running environment of the failed job.
4) When clicked, the dev will be redirected to Mission Control to input the username and the password of the new VM and confirm the creation of it.
5) Mission Control will display information about the status of the VM.
6) When a VM is created, the dev will be notified by:
    - Mission Control status page.
    - Email sent to the dev.

### 2. Rerun a job
![](./Images/Scenario2.png?raw=true)
When a job fails and the environment is not automatically saved, the dev could:
1) Log in into Jenkins.
2) Click on a link to rebuild and repro the job.
3) The parameters of the build will appear and ask the user to input the username and the password of the VM.
4) When the VM is created, the dev will be notified by:
    - Mission Control status page.
    - Jenkins console log.
    - Email sent to the dev.

### Idea of how the Jenkins UI might look like
![](./Images/JenkinsUI.png?raw=true)

### Idea of how the Mission Control UI might look like
![](./Images/MainPage.png?raw=true)

Same design for Snapshots and Virtual Machine information.
All the buttons will trigger a dialog requesting more information.
  - Connect: Information on how to connect to the machine according to the restrictions.
  - Delete: Confirmation.
  - Extend Expiration: Input a number between 1 and 3. There also needs to be a limit on the number of extensions allowed.
  - Create VM: Input `Username` and `Password`.

![](./Images/Snap.png?raw=true)

## Connect to the new VM
The interfaces to connect to the VM will be:
- A link in the Jenkins Job that will redirect to Mission Control.
-  Mission Control status page.
Once in Mission Control, the ways to connect to the machine vary by OS:
- Windows: A link directly to the machine.
- Linux: IP and port will available for the user to connect by SSH to the machine. For the prototype, we are considering using username and password instead of an SSH certificate.

## Use the VM
The following information should be available to the dev:
- Where the workspace is located.
- Machine configuration.
- The time when the machine was created and therefore when the machine is going to be deleted.
- Information on how to extend the period of time for the VM.
- Information on how to signal that the work on the machine is done.

For Windows, the information could be on the desktop as part of the image and with shortcuts while in Linux the files will be in the root.

## Finish work on the VM
- Run a script inside the machine that signals the machine as done.
- Delete the machine directly from Mission Control.
- A running job that automatically deletes the machines according to the retention policies.

# Progress
For information about the progress, please go to the following Epic:
- Dev Test Failure Repro [408](https://github.com/dotnet/core-eng/issues/408)

## Considerations:
Please refer to the section [Developers involved in the POC](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#developers-involved-in-the-poc) of Implementation details POC.
