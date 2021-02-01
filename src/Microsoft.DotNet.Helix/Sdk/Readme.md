# Microsoft.DotNet.Helix.Sdk

This Package provides Helix Job sending functionality from an MSBuild project file.

## Examples
Each of the following examples require dotnet-cli >= 2.1.300 and need the following files in a directory at or above the example project's directory.
#### NuGet.config
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dotnet-eng" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json" />
  </packageSources>
</configuration>
```
#### global.json
```json
{
  "msbuild-sdks": {
    "Microsoft.DotNet.Helix.Sdk": "<version of helix sdk package from package feed>"
  }
}
```

Versions of the package can be found by browsing the feed at https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-eng

The examples can all be run with `dotnet msbuild` and will require an environment variable or MSBuildProperty `HelixAccessToken` set if a queue with a value of IsInternalOnly=true (usually any not ending in '.Open') is selected for `HelixTargetQueues`. You will also need to set the following environment variables before building:

```
BUILD_SOURCEBRANCH
BUILD_REPOSITORY_NAME
SYSTEM_TEAMPROJECT
BUILD_REASON
```

Also, make sure your helix project doesn't have `EnableAzurePipelinesReporter` set, or sets it to false, or building locally will fail with an error that looks like `SYSTEM_ACCESSTOKEN is not set`.

### Docker Support
Helix machines now have (where available on the machine) the ability to run work items directly inside Docker containers.  This allows work items to use operating systems that only work for Docker scenarios, as well as custom configurations of already-supported operating systems.  
#### Specifying a docker tag:
Supported docker tags include anything publicly available on dockerhub.io, as well as azurecr.io and mcr container registries which have had the appropriate service principal users added or are public. In all cases, use the format:
```
({Optional Queue Alias}){Helix Queue Id}@{DockerTag}
```
As an example, to run a typical Helix work item targeting an Alpine 3.9 docker image on a Ubuntu 16.04 host, the queue Id used would be `(Alpine.39.Amd64)ubuntu.1604.amd64.open@mcr.microsoft.com/dotnet-buildtools/prereqs:alpine-3.9-helix-bfcd90a-20200123191053`.
Anywhere a container registry is left out, dockerhub.io is assumed; generally execution is done only on MCR images controlled by the .NET Team, as this allows fine control over the user inside the container and its permissions.
#### Limitations:
- Windows Docker machines will always be set in Windows container mode, as it is non-trivial to reliably switch between these formats.  In general you should use OSX or Linux machines for your non-Windows Docker needs, and a matching RS* level of Windows for the Server used (i.e. to run Nano RS5, you need to run on Server RS5 currently)
- While work items will execute as usual, many Helix work items assume the existence of python 3.7 on the machine and will fail to do certain parts (such as uploading logs and other artifacts) when run.  The ['python' repo on dockerhub](https://hub.docker.com/_/python) provides Python 3 forms of Alpine, Debian, and Windows Server Core images.  Others will need to install python as part of image generation.
- Not all Helix Queues will have Docker installed (or in some cases Docker may be broken there).  Contact the dnceng team if you feel a particular Helix queue should have Docker installed, but does not; the https://helix.dot.net/api/2019-06-17/info/queues API will always serve as the "source of truth" for which machines have the Docker EE installed at any given time.
- To update or add new Helix Docker images, make a pull request to https://github.com/dotnet/dotnet-buildtools-prereqs-docker; updated images are published to dotnet/versions repo.  Image names checked into your sources are not automatically updated, and there is intentionally no "latest" tag.

### Hello World
This will print out 'Hai Wurld!' in the job console log.
```xml
<Project Sdk="Microsoft.DotNet.Helix.Sdk" DefaultTargets="Test">
  <PropertyGroup>
    <HelixSource>pr/testing/</HelixSource>
    <HelixType>test/stuff</HelixType>
    <HelixBuild>23456.01</HelixBuild>
    <HelixTargetQueues>Windows.10.Amd64.Open</HelixTargetQueues>
  </PropertyGroup>

  <ItemGroup>
    <HelixWorkItem Include="Hello World!">
      <Command>echo 'Hai Wurld!'</Command>
    </HelixWorkItem>
  </ItemGroup>
</Project>
```

### Using a Payload folder
Given a local folder `$(TestFolder)` containing `runtests.cmd`, this will run `runtests.cmd`.
```xml
<Project Sdk="Microsoft.DotNet.Helix.Sdk" DefaultTargets="Test">
  <PropertyGroup>
    <HelixSource>pr/testing</HelixSource>
    <HelixType>test/stuff</HelixType>
    <HelixBuild>23456.01</HelixBuild>
    <HelixTargetQueues>Windows.10.Amd64.Open</HelixTargetQueues>
  </PropertyGroup>

  <ItemGroup>
    <HelixWorkItem Include="Using a Payload">
      <Command>runtests.cmd</Command>
      <PayloadDirectory>$(TestFolder)</PayloadDirectory>
    </HelixWorkItem>
  </ItemGroup>
</Project>
```

### All Possible Options
```xml
<Project Sdk="Microsoft.DotNet.Helix.Sdk" DefaultTargets="Test">
  <PropertyGroup>
    <!-- The 'source' value reported to helix  -->
    <HelixSource>pr/testing/</HelixSource>
    <!-- The 'type' value reported to helix  -->
    <HelixType>test/stuff/</HelixType>
    <!-- The 'build' value reported to helix  -->
    <HelixBuild>23456.01</HelixBuild>

    <!-- The helix queue this job should run on. -->
    <HelixTargetQueue>Windows.10.Amd64.Open</HelixTargetQueue>

    <!--
      The set of helix queues to send jobs to.
      This property is multiplexed over just like <TargetFrameworks> for C# projects.
      The project is built once per entry in this list with <HelixTargetQueue> set to the current list element value.
      Note that all payloads sent need to be able to run on all variations included.
    -->
    <HelixTargetQueues>Ubuntu.1804.Amd64.Open;Ubuntu.1604.Amd64.Open;(Alpine.39.Amd64)Ubuntu.1604.Amd64.Open@mcr.microsoft.com/dotnet-buildtools/prereqs:alpine-3.9-helix-bfcd90a-20200123191053</HelixTargetQueues>

    <!-- 'true' to download dotnet cli and add it to the path for every workitem. Default 'false' -->
    <IncludeDotNetCli>true</IncludeDotNetCli>
    <!-- 'sdk', 'runtime' or 'aspnetcore-runtime' -->
    <DotNetCliPackageType>sdk</DotNetCliPackageType>
    <!-- 'latest' or a specific version of dotnet cli -->
    <DotNetCliVersion>2.1.403</DotNetCliVersion>
    <!-- 'Current' or 'LTS', determines what channel 'latest' version pulls from -->
    <DotNetCliChannel>Current</DotNetCliChannel>

    <!-- Enable reporting of test results to azure dev ops -->
    <EnableAzurePipelinesReporter>false</EnableAzurePipelinesReporter>
    <!-- 'true' to produce a build error when tests fail. Default 'true' -->
    <FailOnTestFailure>true</FailOnTestFailure>

    <!--
      'true' to enable the xunit reporter. Default 'false'
      The xunit reporter will report test results from a test results
      xml file found in the work item working directory.
      The following file names are accepted:
        testResults.xml
        test-results.xml
        test_results.xml
    -->
    <EnableXUnitReporter>false</EnableXUnitReporter>
    <!-- Instruct the sdk to wait for test result ingestion by MC, and fail if there are any failed work items or tests. -->
    <FailOnMissionControlTestFailure>false</FailOnMissionControlTestFailure>

    <!--
      Commands that are run before each workitem's command
      semicolon-separated; use ';;' to escape a single semicolon
    -->
    <HelixPreCommands>$(HelixPreCommands);echo 'pizza'</HelixPreCommands>

    <!--
      Commands that are run after each workitem's command
      semicolon separated; use ';;' to escape a single semicolon
    -->
    <HelixPostCommands>$(HelixPostCommands);echo 'One Pepperoni Pizza'</HelixPostCommands>
  </PropertyGroup>

  <!--
    XUnit Runner
      Enabling this will create one work item for each xunit test project specified.
      This is enabled by specifying one or more XUnitProject items
  -->
  <ItemGroup>
    <XUnitProject Include="..\tests\foo.Tests.csproj"/>
  </ItemGroup>
  <PropertyGroup>
    <!-- TargetFramework to publish the xunit test projects for -->
    <XUnitPublishTargetFramework>netcoreapp3.1</XUnitPublishTargetFramework>
    <!-- TargetFramework of the xunit.runner.dll to use when running the tests -->
    <XUnitRuntimeTargetFramework>netcoreapp2.0</XUnitRuntimeTargetFramework>
    <!-- PackageVersion of xunit.runner.console to use -->
    <XUnitRunnerVersion>2.4.1</XUnitRunnerVersion>
    <!-- Additional command line arguments to pass to xunit.console.exe -->
    <XUnitArguments></XUnitArguments>
  </PropertyGroup>


  <ItemGroup>
    <!--
      Another way to specify target queues
      This can be used to specify more properties to use for each queue.
    -->
    <HelixTargetQueue Include="Windows.10.Amd64.Open">
      <AdditionalProperties>Platform=x64;Configuration=Debug</AdditionalProperties>
    </HelixTargetQueue>

    <!-- Directory that is zipped up and sent as a correlation payload -->
    <HelixCorrelationPayload Include="some\directory\that\exists" />

    <!-- Workitem that is run on a machine from the $(HelixTargetQueue) queue -->
    <HelixWorkItem Include="some work item name">
      <!-- Command that runs the work item -->
      <Command>echo 'sauce'</Command>

      <!-- A directory that is zipped up and sent as the work item payload -->
      <PayloadDirectory>$(TestFolder)</PayloadDirectory>

      <!-- A TimeSpan that specifies the work item execution timeout -->
      <Timeout>00:30:00</Timeout>

      <!-- Commands that will run before the work item command -->
      <PreCommands>echo 'pepperoni';echo 'cheese'</PreCommands>

      <!-- Commands that will run after the work item command -->
      <PostCommands>echo 'crust';echo 'oven'</PostCommands>
    </HelixWorkItem>
  </ItemGroup>
</Project>
```

### iOS/Android/WASM workload support (XHarness)
The Helix SDK also supports execution of Android/iOS/WASM workloads where you only need to point it to an Android .apk or an iOS/tvOS/WatchOS .app bundle and it will execute these using a tool called XHarness on a specified emulator/device/JS engine. The workloads have to run on Helix queues that are ready for these types of jobs, meaning they have emulators installed, devices connected or JS engine installed. You can read more about this [here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/tools/xharness-runner/Readme.md).

### Custom Helix WorkItem functionality
There are times when a work item may detect that the machine being executed on is in a (possibly transient) undesirable state.  Additionally there can be times when a work item would like to request its machine be rebooted after execution (for instance, when a file handle is mysteriously open from another process).  The following functionality has been added to request both of these and can be used either from within a python script or any command line.

#### Request Infrastructure Retry
An "infrastructure retry" is pre-existing functionality Helix Clients use in cases such as when communication to the telemetry service or Azure Service Bus fails; this allows the work item be run again in entirety, generally (but not guaranteedly) on a different machine, with the hope that the next machine will be in a better state.  Note that requesting this prevents any job using it from finishing, and as a FIFO queue the work items that get retried go to the back of the queue, so calling this API can significantly increase job execution time based off how many jobs are being handled by a given queue.      

##### Sample usage:

###### In Python:

```
from helix.workitemutil import request_infra_retry

request_infra_retry('Optional reason string')

```

###### Outside python:

Linux / OSX: `$HELIX_PYTHONPATH -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Optional reason string')"`

Windows: `%HELIX_PYTHONPATH% -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Optional reason string')"`

#### Request post-workitem reboot
Helix work items explicitly rebooting the helix client machine themself will never "finish", since this will in most cases preclude sending the final event telemetry for these work items.  However, a work item may know that the machine is in a bad state where a reboot would be desirable (for instance, if the Helix agent is acting as a build machine and some leaked build process is preventing workspace cleanup). After calling this API, the work item runs to completion as normal, then after sending the usual telemetry and uploading results will perform a reboot before taking the next work item. 

##### Sample usage:

###### In Python:

```
from helix.workitemutil import request_reboot

request_reboot('Optional reason string')

```

###### Outside python:

Linux / OSX: `$HELIX_PYTHONPATH -c "from helix.workitemutil import request_reboot; request_reboot('Optional reason string')"`

Windows: `%HELIX_PYTHONPATH% -c "from helix.workitemutil import request_reboot; request_reboot('Optional reason string')"`

### Common Helix client environment variables

When possible, constructing paths for scripts / commands executed within Helix work items should be done using the provided environment variables, allowing for the engineering team to move and optimize placement of these folders without breaking execution.

You may assume that all the following variables are set on any given Helix client. (Use appropriate-for-OS means to access, i.e. %WINDOWS% or $Linux, $OSX).  The list is not exhaustive but most other variables are simply uninteresting from the perspective of the work item.

- **HELIX_CORRELATION_ID** : GUID identifier for a helix run (include this if sending mail to or tagging dnceng)
- **HELIX_CORRELATION_PAYLOAD** : Correlation payload folder;  root of where all correlation payloads are unzipped.
- **HELIX_PYTHONPATH** : Path to a python 3.x executable (Due to OS constraints, this is only guaranteed to be >= 3.4)
- **HELIX_WORKITEM_FRIENDLYNAME** - "Friendly" name of work item as provided at queue time (include this if relevant when sending mail to or tagging dnceng)
- **HELIX_WORKITEM_ID** : GUID identifier for a helix work item 
- **HELIX_WORKITEM_PAYLOAD** : "Unzip" folder of helix workitem, where its payload was unpacked
- **HELIX_WORKITEM_ROOT** : "Execution" folder of helix workitem, where its payload is copied to and run
- **HELIX_WORKITEM_UPLOAD_ROOT** : Any file in this folder at the end of the work item will be uploaded to result storage and made available via Helix API / backing database.
- **HELIX_DUMP_FOLDER** : Process dumps created here will get uploaded and automatically cleaned up
- **HELIX_CURRENT_LOG** : Path to the current work item's console log (note: will typically have file handles open)


