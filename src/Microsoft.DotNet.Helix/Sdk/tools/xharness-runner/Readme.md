# XHarness support in Microsoft.DotNet.Helix.Sdk

> This document presumes you are familiar with the usage of Microsoft.DotNet.Helix.Sdk. If not, please [start here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/Readme.md).

The Helix SDK supports execution of certain **Android/iOS/tvOS/WatchOS/WASM workloads** where you only need to point the SDK to:
  - Android .apks,
  - iOS/tvOS/WatchOS .app bundles,
  - WASM-ready test DLLs

and it will execute these for you.

The SDK will create a Helix job with the specified payload and send it to Helix where, using a tool called [XHarness](https://github.com/dotnet/xharness), it will find a suitable test target - an emulator, a real device or a specified JS engine for WASM scenarios - which it will run the workload on.

For these workloads, we currently expect the payload to contain xUnit tests and an [XHarness TestRunner](https://github.com/dotnet/xharness#test-runners) which will run these tests once the application is started.
Logs will be collected automatically and sent back with the other Helix results.
The test results themselves can be published to Azure DevOps using the same python-based publishing scripts as regular Helix jobs.

XHarness is a [.NET Core tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) and requires **.NET Core 3.1 runtime** to execute on the Helix agent.
This is automatically included as a Helix Correlation Payload for the job when XHarness workload is detected.

## How to use

There are three main ways how to use XHarness through the Helix SDK:
- Specify the apks/app bundles using the `XHarnessApkToTest` and `XHarnessAppBundleToTest` items as described below and everything will be taken care of from there. You no longer specify the `HelixCommand` to be executed. Each apk/app bundle will be processed as a separate Helix work item.
- Specify the `XHarnessAndroidProject` or `XHarnessAppleProject` task items which will point to projects that produce apks/app bundles from their `Build` target.
  - Examples - [iOS](https://github.com/dotnet/arcade/blob/master/tests/XHarness/XHarness.TestAppBundle.proj) and [Android](https://github.com/dotnet/arcade/blob/master/tests/XHarness/XHarness.TestApk.proj)
- Only request the XHarness dotnet tool to be pre-installed for the Helix job for you and then call the XHarness tool yourself as shown below.

There are some required configuration properties that need to be set for XHarness to work and some optional to customize the run further:

```xml
<PropertyGroup>
  <!-- Required: Version of XHarness CLI to use -->
  <IncludeXHarnessCli>true</IncludeXHarnessCli>

  <!-- Required: Version of XHarness CLI to use. Check the NuGet feed for current version: https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-eng&package=Microsoft.DotNet.XHarness.CLI&protocolType=NuGet -->
  <MicrosoftDotNetXHarnessCLIVersion>1.0.0-prerelease.20322.1</MicrosoftDotNetXHarnessCLIVersion>

  <!-- Optional: Properties that are also valid for the Arcade Helix SDK (some might be needed for CI runs only) -->
  <HelixType>test/product/</HelixType>
  <HelixBaseUri>https://helix.int-dot.net</HelixBaseUri>
  <Creator>$(BUILD_SOURCEVERSIONAUTHOR)</Creator>
  <EnableXUnitReporter>true</EnableXUnitReporter>
  <EnableAzurePipelinesReporter>true</EnableAzurePipelinesReporter>
</PropertyGroup>

<!-- Required: Configuration that is already needed for the Helix SDK -->
<ItemGroup Condition=" '$(HelixAccessToken)' == '' ">
  <HelixTargetQueue Include="osx.1015.amd64.open"/>
</ItemGroup>
```

### Calling the XHarness tool directly

In case you decide to request the SDK to pre-install the XHarness tool only without any specific payload, you just don't specify `XHarnessApkToTest` or `XHarnessAppBundleToTest` items and you specify the Helix command directly.
There will be an environmental variable called `XHARNESS_CLI_PATH` set that will point to the XHarness CLI DLL that needs to be run using `dotnet exec` like so:

```xml
<ItemGroup>
  <HelixWorkItem Include="Run WASM tests">
    <Command Condition="$(IsPosixShell)">dotnet exec $XHARNESS_CLI_PATH wasm test --engine ...</Command>
    <Command Condition="!$(IsPosixShell)">dotnet exec %XHARNESS_CLI_PATH% wasm test --engine ...</Command>
  </HelixWorkItem>
</ItemGroup>
```

### iOS/tvOS/WatchOS .app bundle payloads

To execute .app bundles, declare one or more `XHarnessAppBundleToTest` items:

```xml
<ItemGroup>
  <!-- Find all directories named *.app -->
  <XHarnessAppBundleToTest Include="$([System.IO.Directory]::GetDirectories('$(TestArchiveTestsRoot)', '*.app', System.IO.SearchOption.AllDirectories))">
    <Targets>ios-simulator-64_13.5</Targets>
  </XHarnessAppBundleToTest>
</ItemGroup>
```

The `<Targets>` metadata is a required configuration that tells XHarness which kind of device/Simulator to target.
Use the XHarness CLI help command to find more (see the `--targets` option).

You can also specify some metadata that will help you configure the run better:

```xml
<ItemGroup>
  <XHarnessAppBundleToTest Include=".\appbundles\Contoso.Example.Tests.app">
    <!-- Optional: Timeout for the overall run of the whole Helix work item (including Simulator booting, app installation..) -->
    <WorkItemTimeout>00:20:00</WorkItemTimeout>

    <!-- Optional: Timeout for the actual test run (when TestRunner starts execution of tests) -->
    <!-- Should be smaller than WorkItemTimeout by several minutes -->
    <TestTimeout>00:12:00</TestTimeout>

    <!-- Optional: Timeout for how long it takes to install and boot the app and start running the first test -->
    <LaunchTimeout>00:10:00</LaunchTimeout>

    <!-- Optional (`apple run` command only): Expected exit code of the iOS/tvOS application. XHarness exits with 0 when the app exits with this code -->
    <!-- Please note that exit code detection may not be reliable across iOS/tvOS versions -->
    <ExpectedExitCode>3</ExpectedExitCode>
    
    <!-- Optional: For apps that don't contain unit tests, they can be run using the `apple run` command instead of `apple test` -->
    <!-- Default is true -->
    <IncludesTestRunner>false</IncludesTestRunner>
  </XHarnessAppBundleToTest>
</ItemGroup>
```

You can configure the execution further via MSBuild properties:

```xml
<PropertyGroup>
  <!-- Optional: Specific version of Xcode to use. If omitted, xcode-select is used to determine the version -->
  <XHarnessXcodeVersion>11.4</XHarnessXcodeVersion>
</PropertyGroup>
```

#### Targeting real iOS/tvOS devices

To deploy an app bundle to a real device, the app bundle needs to be signed before the deployment.
The Helix machines, that have devices attached to them, already contain the signing certificates and a provisioning profile will be downloaded as part of the job.

When using the Helix SDK and targeting real devices:
- You have to ideally supply a non-signed app bundle - the app will be signed for you on the Helix machine where your job gets executed
- Only the basic set of app permissions are supported at the moment and we cannot re-sign an app that was already signed with a different set of permissions
- Bundle id has to start with `net.dot.` since we only support those application IDs at the moment

### Android .apk payloads

To execute .apks, declare one or more `XHarnessApkToTest` items:

```xml
<ItemGroup>
  <XHarnessApkToTest Include="$(TestArchiveTestsRoot)apk\x64\System.Numerics.Vectors.Tests.apk">
    <!-- Package name: this comes from metadata inside the apk itself -->
    <AndroidPackageName>net.dot.System.Numerics.Vectors.Tests</AndroidPackageName>

    <!-- If there are > 1 instrumentation classes inside the package, we need to know the name of which to use -->
    <AndroidInstrumentationName>net.dot.MonoRunner</AndroidInstrumentationName>
  </XHarnessApkToTest>
</ItemGroup>
```

You can also specify some metadata that will help you configure the run better:

```xml
<ItemGroup>
  <XHarnessApkToTest Include="$(TestArchiveTestsRoot)**\*.apk">
    <!-- Optional: Timeout for the overall run of the whole Helix work item (including Simulator booting, app installation..) -->
    <WorkItemTimeout>00:20:00</WorkItemTimeout>

    <!-- Optional: Timeout for the actual test run (when TestRunner starts execution of tests) -->
    <!-- Should be smaller than WorkItemTimeout by several minutes -->
    <TestTimeout>00:12:00</TestTimeout>
  
    <!-- Optional: Expected exit code of the instrumentation run. XHarness exits with 0 when the app exits with this code -->
    <ExpectedExitCode>3</ExpectedExitCode>
  </XHarnessApkToTest>
</ItemGroup>
```

### WASM payloads

We currently do not support execution of WASM workloads directly, please call the `xharness wasm test` command manually.
