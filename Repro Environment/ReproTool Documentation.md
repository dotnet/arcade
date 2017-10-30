#  Repro Tool Documentation
The following document shows the user documentation of the repro environment solution according to the [requirements](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Prod%20Requirements.md).

### Known Issues
- [1708](https://github.com/dotnet/core-eng/issues/1708) Information in windows is not showing up on start.
- [1649](https://github.com/dotnet/core-eng/issues/1649) Jenkins is still running on windows after creating a snapshot and a VM.
- [1487](https://github.com/dotnet/core-eng/issues/1487) Delete a VM in Mission Control sometimes reports error.
- [1694](https://github.com/dotnet/core-eng/issues/1694) Reconsider the user experience when clicking Repro in Jenkins.

# What is the Repro Tool?
When a job fails in [Jenkins] or [Helix], the developer may ask for a virtual machine or Mac machine with the sufficient data and access to connect and build the code that is located in the original machine to determine why the job failed.

# How to use it in Helix
When a test that was ran in Helix fails, go to [Mission Control Int] to the page where you can see the logs of the test. You would see a new link `Get Repro environment`. Once clicked, it would provide a set of instructions to follow in order to get the repro.

![](./Images/Helix-ReproLink.PNG?raw=true)

Once the execution of the `helix-repro.exe` ends, go to [Mission Control Int].
See Mission Control section for more informatiom.

The Repro tool will save the log of the jobs that were executed as part of the repro process. In case there is need to check them, go to the Private Runs page in [Mission Control Int] and look for the build identified by the buildnumber (aaa/mm/dd).vv Make sure to uncheck the `Show only failures` checkbox to get to the logs of the run.

## I'm in the machine, now what?

### Windows
The workspace and files that contain information of the machine are located under the `D:\` directory. Go inside the workspace to start debugging the problem.

### Linux
Run `source /home/helixbot/repro.sh` to set some environment variables and get to the directory where the test was executed. You'll find the executables, the dll, pdb, logs, etc.

### Mac
Run `source repro.sh` to set some environment variables and get to the directory where the test was executed. You'll find the executables, the dll, pdb, logs, etc.

# How to use it in Jenkins
When a job inside a PR fails, click on details to go to Jenkins and **Log In**. 
Once logged in, there is going to be a new link by the name of `Repro`. Please note that if a job doesn't fail there is no way to `Repro` it.

![](./Images/ReproLink.PNG?raw=true)

If the `Repro` link is clicked, a new job is going to start and will contain the same parameters the triggering job had and at the end, it will create a repro environment to be consumed by the user.

## How to know which Job is my Repro Job?
The new job will say `Repro Job from build #<build number> requested by user <Github username>` as a description of it.
![](./Images/ReproJob.PNG?raw=true)

The job will finish with the creation of a VM. 

A link in the left side of the screen under the name `Connect to Repro VM` will let the user know that the environment is ready to consume. This link is going to redirect the user to [Mission Control Int] where the VM can be accessed.

Please note that **Log in** is required in order to see the link.

![](./Images/ConnectToVmLink.PNG?raw=true)

See Mission Control section for more information on how to connect to the machine.

## I'm in the machine, now what?
The workspace and files that contain information of the machine are located under the `D:\` directory. Go inside the workspace to start debugging the problem.

Some of the files available are:
- Environment.txt
- Installed.txt
- Machine.txt

# Mission Control
In order to see the information in [Mission Control Int], the user must **Log in**, otherwhise no information will be available. Under the Repro tab you'll find all the information about the requested environments that are still active.

Go to [Mission Control Int], **Log In** and go to the `Repros` tab. 
![](./Images/ReproTab.PNG?raw=true)

By default the user can see all the [Snapshots](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#2-save-running-environment) and [Virtual Machines](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#4-create-vm-with-repro-environment) created by that user.

![](./Images/MissionControl.PNG?raw=true)

Please note that the Virtual Machines are only available to the user that created the machine, so if you search for other users environment, there is no way to see their VMs.

To access a machine, click on `Connect`. For Windows and Linux it will download a `.rdp` or `.sh` file and will display the password of the machine.

![](./Images/ConnectToVM.PNG?raw=true)

For a Mac machine, `Connect` will download a `.txt` file with the instructions on how to connect to the machine.

## How to signal that I am done with the machine?
Go back to [Mission Control Int], find the machine and click on `Delete`. 
Please note that both the Snapshots and the Machines expire after 3 days of being created, so in case you forget to delete them, we will delete them for you withouth previous notice.

# How to give feedback?
- Please create a new issue in the [core-eng](https://github.com/dotnet/core-eng) repository.
- Other way is to use the `thumbs up/down` in Mission Control when located in the Repros tab. 

![](./Images/FeedbackMC.png?raw=true)

[Mission Control Int]: https://mc.int-dot.net/#/
[Helix]: https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/ReproTool%20Documentation.md#how-to-use-it-in-Helix
[Jenkins]: https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/ReproTool%20Documentation.md#how-to-use-it-in-Jenkins
