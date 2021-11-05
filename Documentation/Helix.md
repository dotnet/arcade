# Helix

Helix is a system/service that allows for very broad scaling of workloads and their results in a reliable and low cost fashion. The Azure back-end allows for cloud-level scalability without needing to maintain the underlying infrastructure.

The [Helix C# Client](/src/Microsoft.DotNet.Helix/Client/CSharp) and [JobSender](/src/Microsoft.DotNet.Helix/JobSender) library can be used to programmatically interact with Helix.

## Goals

* System can run any arbitrary work (not just .NET)
* Have a work execution system leveraging the cloud that enables us to deliver our products across all platforms without a dependency on internal Microsoft resources.
* Create a componentized system where each piece could be swapped out for something else
* Each component only requires HTTPS access
* Leverage, as much as is possible, released Microsoft technology

## What Helix does for us

- Distributed work execution

	- Helix machines perform arbitrary work on custom-curated, sometimes-scalable machines. When users want to test on other OSes, or run large quantities of work across many machines even from the same OS, Helix enables distributed execution on dozens of different operating systems with work specified in the same, simple JSON-based format for all operating systems. Supported operating systems include macOS, various Windows configurations (Client and server SKUs, non-en-us languages, and variations), Linux systems, with the option to expand to anywhere Python 3 is supported.

- Telemetry

    - When an Engineering-Services-supported Azure DevOps pipeline kicks off a build or test run, telemetry is automated-ly gathered and sent through the Helix pipeline to be stored and reported. 
    - This is accomplished by using Azure DevOps tasks which can send telemetry directly to the Helix EventHub, as well as being directly sent by the various machines performing work.
    - Telemetry is gathered and reported in near-real-time to provide minute-by-minute build and test updates, monitoring things like exit code, console and harness logs, output files, and even crash dumps.   

- Helix API

	- The Helix API allows users to submit work and telemetry, query previously run work items and jobs for logging and aggregation of results.
	- [Helix's Client libraries](/src/Microsoft.DotNet.Helix) are used by most consumers whether it be for submitting work items, sending telemetry, or waiting on and determining the results of a run.
	- Anonymous execution and machines are supported, as well as authenticated work using a GitHub authentication token. Sensitive workloads can be locked down to one or more GitHub user names, which must match to use these queues. 
	- Users are also able to directly interact with the Helix API via its swagger endpoints in both anonymous and authenticated fashion.

Additional Helix implementation details are available in the private core-eng repository: https://github.com/dotnet/core-eng/blob/main/Documentation/HelixDocumentation.md

## Features of Helix

### Job Distribution at Scale
With Helix we leverage the power of Azure to create and remove VMs as needed.  This allows us to create the scale we want at whatever budget we can afford.  We also have the ability to add physical machines for other scenarios such as executing work on macOS, ARM, ARM64, dedicated physical hardware for performance, and on virtually any device capable of running Python 3.5 or greater with internet connectivity.

### Supports Cloud and On-Premise Solutions
When designing Helix our goal was to leverage the cloud in a way that gave us the reliability we needed, the performance we desired, and simplicity throughout.  For this we dove into various cloud-based solutions and landed on Azure. Azure gives us the ability to have reliable services that we can communicate to over HTTPS. This gives us the flexibility to have either a full cloud-only solution or to have a hybrid solution with workers that can be on-premises.

### Cross Platform Support
A major requirement for this system was to be able to execute work across multiple platforms.  A wide, constantly-growing variety of operating systems are available, generally removed within a month or two of end-of-life.  To see this list live, visit <https://helix.dot.net/>.

### Docker Image Support
For any Helix queue where Docker is installed (viewable via the Queue info API), work items may be executed in a docker image via the syntax `(Fake.Queue.Name)real.queue.name@docker.tag`.  This allows running on OS combinations we either can't install on Azure (e.g. Fedora Linuxes) or which only support docker (Windows Nano, Alpine Linux, and others)

## Usage

### API Endpoints
To submit work to Helix you need to call an API endpoint with a JSON blob that describes the work you would like executed.  This JSON blob will then be broken out among as many machines as are available.  

API Definition: <https://helix.dot.net/swagger/ui/index.html#/Job/Job_New>

- For small console-app style usages, refer to the [documentation for how to write a small Helix sender tool](/src/Microsoft.DotNet.Helix/JobSender)
- For MSBuild usage, refer to the [documentation of the Helix SDK](/src/Microsoft.DotNet.Helix/Sdk) or any Arcade repo themselves, which all use this SDK to send test work items 

#### Helpful functionality
Both from within a python script and from any other type of execution being performed on a helix client machine, there are operations you might need to perform using python functionality on the client.  This is not an exhaustive list; if you need functionality you feel should be provided on the client and can't find it, please contact dnceng.

##### Automatically Upload Files to Azure Storage
Any files contained within the HELIX_WORKITEM_UPLOAD_ROOT folder will be automatically uploaded to Azure Storage results container (available via a variable called **ResultUri**). For best performance files should be created in here, but failing that simply copying them into this path before finishing the work item will persist them, which will then be accessible after the work item completes from [the ListFiles API](https://helix.dot.net/swagger/ui/index.html#/WorkItem/WorkItem_ListFiles).

##### Disable Dialog Handler (Windows only)
Dialog Handler (dhandler.exe) runs by default on any Helix Windows machine that has UI.  This prevents timeouts caused by the unintentional display of a modal dialog or other UI, but makes things difficult if your work item needs to interact with UI on its own (e.g. UI-based testing, VS extension tests)

To work around from Python:

```
from helix_test_execution import disable_dialog_handler 
...
disable_dialog_handler()
``` 

from other contexts:
```
%HELIX_PYTHONPATH% -c "from helix_test_execution import disable_dialog_handler; disable_dialog_handler()"
```

Finally if neither of these suits you, simply killing dhandler.exe on the machine is a fine alternative; the process will be restarted in the event of a work item terminating it, accidentally or otherwise, on the next work item's start. 

### Sample App
To try out a sample app that sends a very basic job through the system, refer to the aforementioned [documentation for how to write a small Helix sender tool](/src/Microsoft.DotNet.Helix/JobSender).

To add a reference to the appropriate Nuget Packages, you may find you need to add `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json` as a Nuget Package source and look up the package names explicitly.
