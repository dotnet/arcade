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
    <add key="dotnet-tools" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json" />
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

Versions of the package can be found by browsing the feed at https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-tools

The examples can all be run with `dotnet msbuild` and will require an environment variable or MSBuildProperty `HelixAccessToken` set if a queue with a value of IsInternalOnly=true (usually any not ending in '.Open') is selected for `HelixTargetQueues`

### Docker Support
Helix machines now have (where available on the machine) the ability to run work items directly inside Docker containers.  This allows work items to use operating systems that only work for Docker scenarios, as well as custom configurations of already-supported operating systems.  
#### Specifying a docker tag:
Supported docker tags include anything publicly available on dockerhub.io, as well as azurecr.io and mcr container registries which have had the appropriate service principal users added or are public. In all cases, use the format:
```
{Helix Queue Id}@{DockerTag}
```
As an example, to run a typical Helix work item targeting a python-enabled Alpine 3.8 docker image on a Ubuntu 16.04 host, the queue Id used would be `Ubuntu.1604.Amd64@python:2.7.15-alpine3.8`.  (see below why the "python" repo is used for the example)
Anywhere a container registry is left out, dockerhub.io is assumed; the above could just as easily be listed as `Ubuntu.1604.Amd64@dockerhub.io/python:2.7.15-alpine3.8`
#### Limitations:
- Currently Windows Docker machines will always be set in Windows containers mode, as it is non-trivial to reliably switch between these formats.  In general you should use OSX or Linux machines for your non-Windows Docker needs, and a matching RS* level of Windows for the Server used (i.e. to run Nano RS4, you need to run on Server RS4 currently)
- While work items will execute as usual, many Helix work items assume the existence of python 2.7 on the machine and will fail to do certain parts (such as uploading logs and other artifacts) when run.  The ['python' repo on dockerhub](https://hub.docker.com/_/python) provides Python 2.7 forms of Alpine, Debian, and Windows Server Core images.  Others will need to be published either to dockerhub or elsewhere.
- Not all Helix Queues will have Docker installed (or in some cases Docker may be broken there).  Contact the dnceng team if you feel a particular Helix queue should have Docker installed, but does not.

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
      
    -->
    <HelixTargetQueues>Ubuntu.1804.Amd64.Open;Ubuntu.1604.Amd64.Open</HelixTargetQueues>

    <!-- 'true' to download dotnet cli and add it to the path for every workitem. Default 'false' -->
    <IncludeDotNetCli>true</IncludeDotNetCli>
    <!-- 'sdk' or 'runtime' -->
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
    <XUnitPublishTargetFramework>netcoreapp2.1</XUnitPublishTargetFramework>
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
