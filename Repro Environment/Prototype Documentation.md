#  Prototype Documentation
The following document shows the user documentation for the prototype of the repro environment solution according to the [requirements](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20Prototype.md#requirements).

### Known Issues
- [1580](https://github.com/dotnet/core-eng/issues/1580) Repro pluging should build with parent job parameters.
- [1487](https://github.com/dotnet/core-eng/issues/1487) Delete a VM in MC always reports error.

# What is the Repro Tool?
When a job fails in Jenkins, the developer may ask for a virtual machine with the sufficient data and access to connect and build the code that is located in the machine to determine why the job failed.

The systems that are interacting with the Repro Tool are:
## Jenkins
Once a job fails, the user needs to Log In and look for the job. There is going to be a new link by the name of `Repro`.

![](./Images/ReproLink.PNG?raw=true)

When triggered, the job is going to execute again and then create a repro environment.

The job will finish with the creation of a VM. 

A link in the left side of the screen will redirect the user to Mission Control and the ability to connect to the machine created.

![](./Images/ConnectToVmLink.PNG?raw=true)

Please note the Prototype is currently located at [Jenkins ci4](https://dotnet-ci4.westus2.cloudapp.azure.com/)

## Mission Control
The `Repros` tab contains all the [Snapshots](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#2-save-running-environment) and [Virtual Machines](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/Repro%20Environment/Implementation%20Details%20POC.md#4-create-vm-with-repro-environment) for the user.

![](./Images/ReproTab.PNG?raw=true)

Please note the Prototype is currently located at [Mission Control Int](https://mc.int-dot.net/#/)

# How to give feedback?
- Please create a new issue in the [core-eng](https://github.com/dotnet/core-eng) repository.
- Other way is to use the `thumbs up/down` in Mission Control when located in the Repros tab. 

![](./Images/FeedbackMC.png?raw=true)
