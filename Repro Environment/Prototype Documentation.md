#  Prototype Documentation
The following document shows the user documentation for the prototype of the repro environment solution according to the [requirements](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20Prototype.md#requirements).

### Known Issues
- [1708](https://github.com/dotnet/core-eng/issues/1708) Information in windows is not showing up on start.
- [1649](https://github.com/dotnet/core-eng/issues/1649) Jenkins is still running on windows after creating a snapshot and a VM.
- [631](https://github.com/dotnet/core-eng/issues/631) As a user of Mission Control, pages that require authentication force authentication.
- [1487](https://github.com/dotnet/core-eng/issues/1487) Delete a VM in Mission Control always reports error.
- [1702](https://github.com/dotnet/core-eng/issues/1702) Show error returned by the API in the MC UI.
- [1694](https://github.com/dotnet/core-eng/issues/1694) Reconsider the user experience when clicking Repro in Jenkins.

# What is the Repro Tool?
When a job fails in Jenkins, the developer may ask for a virtual machine with the sufficient data and access to connect and build the code that is located in the machine to determine why the job failed.

## How to use it
When a job inside a PR fails, click on details to go to Jenkins and **Log In**. 
Once logged in, there is going to be a new link by the name of `Repro`. Please note that if a job doesn't fail there is no way to `Repro` it.

![](./Images/ReproLink.PNG?raw=true)

If the `Repro` linked is clicked, a new job is going to start and will contain the same parameters the triggering job had and at the end, it will create a repro environment to be consumed by the user.

## How to know which Job is my Repro Job?
The new job will have `ReproToolCause that determines if the build is a repro build` as a description of it.
![](./Images/ReproJob.PNG?raw=true)


The job will finish with the creation of a VM. 

A link in the left side of the screen under the name `Connect to Repro VM` will let the user know that the environment is ready to consume. This link is going to redirect the user to [Mission Control Int] where the VM can be accessed.

Please note that **Log in** is required in order to see the link

![](./Images/ConnectToVmLink.PNG?raw=true)

In order to see the information in [Mission Control Int], the user must **Log in**, otherwhise no information will be available.

![](./Images/VMDetail.PNG?raw=true)

To access the machine, click on `Connect` (1), this will download a `.rdp` or `.sh` file. To access the password of the machine, click on the button `Password` (2).

![](./Images/ConnectToVM.PNG?raw=true)

## I'm in the machine, now what?
The workspace plus files that contain information of the machine are located under the `D:\` directory. Go inside the workspace to start debugging the problem.

Some of the files available are:
- Environment.txt
- Installed.txt
- Machine.txt

## How to signal that I am done with the machine?
Go back to [Mission Control Int], find the machine and click on `Delete`. 
Please note that both the Snapshots and the Machines expire after 3 days of being created, so in case you forget to delete them, we will delete them for you.

## How to see all the Snapshots and/or Virtual Machines I've created
Go to [Mission Control Int], **Log In** and go to the `Repros` tab. 
![](./Images/ReproTab.PNG?raw=true)

By default the user can see all the [Snapshots](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#2-save-running-environment) and [Virtual Machines](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#4-create-vm-with-repro-environment) created by that user.

![](./Images/MissionControl.PNG?raw=true)

Please note that the Virtual Machines are only available to the user that created the machine, so if you search for other users environment, there is no way to see their VMs.


# How to give feedback?
- Please create a new issue in the [core-eng](https://github.com/dotnet/core-eng) repository.
- Other way is to use the `thumbs up/down` in Mission Control when located in the Repros tab. 

![](./Images/FeedbackMC.png?raw=true)

[Mission Control Int]: https://mc.int-dot.net/#/
