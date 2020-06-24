# XHarness support in Microsoft.DotNet.Helix.Sdk

> This document presumes you are familiar with the usage of Microsoft.DotNet.Helix.Sdk. If not, please [start here](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/Readme.md).

The Helix SDK supports execution of certain **Android/iOS/tvOS/WatchOS/WASM workloads** where you only need to point the SDK to:
  - an Android .apk,
  - an iOS/tvOS/WatchOS .app bundle,
  - or a WASM-ready test DLLs

and it will execute these for you.

The SDK will create a Helix job with the specified payload and send it to Helix, where, using a tool called [XHarness](https://github.com/dotnet/xharness), it will find a suitable test target - an emulator, a real device or a specified JS engine for WASM scenarios - which it will run the workload on.

For these workloads, we currently expect the payload to contain xUnit tests and an [XHarness TestRunner](https://github.com/dotnet/xharness#test-runners) which will run these tests once the application is started.
Logs will be collected automatically and sent back with the other Helix results.
The test results themselves can be published to Azure DevOps just like it is supported with regular Helix jobs.

XHarness is a [.NET Core tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools) and requires **.NET Core 3.1 runtime** to execute on the Helix agent.
This is automatically pre-installed for the job when XHarness workload is detected.

## How to use

There are 3 main ways how to use XHarness through the Helix SDK:
- Specify the apks/app bundles as described above and everything will be taken care of from there. You no longer specify the `HelixCommand` to be executed. Each apk/app bundle you specify will be processed as a separate Helix work item.
- Specify the `XHarnessAndroidProject` or `XHarnessiOSProject` task items which will produce apks/app bundles from the `Build` target.
  - Examples - [iOS](https://github.com/dotnet/arcade/blob/master/tests/XHarness/XHarness.TestAppBundle.proj) and [Android](https://github.com/dotnet/arcade/blob/master/tests/XHarness/XHarness.TestApk.proj)
- Only request the XHarness dotnet tool to be pre-installed for the Helix job for you and then call it yourself from bash/cmd - see [XHarness tool pre-installation only](#xharness-tool-pre-installation-only).

There are some required/optional configuration properties that need to/can be set in any case:

```xml
<PropertyGroup>
  <!-- Required: Version of XHarness CLI to use -->
  <MicrosoftDotNetXHarnessCLIVersion>1.0.0-prerelease.20322.1</MicrosoftDotNetXHarnessCLIVersion>

  <!-- Optional: Properties that are also valid for the Arcade Helix SDK -->
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

### iOS/tvOS/WatchOS .app bundle payloads

To execute .app bundles, declare the `XHarnessAppFolderToTest` items:

```xml
<ItemGroup>
  <!-- Find all directories named *.app -->
  <XHarnessAppFolderToTest Include="$([System.IO.Directory]::GetDirectories('$(TestArchiveTestsRoot)', '*.app', System.IO.SearchOption.AllDirectories))">
    <Targets>ios-device</Targets>
    <Targets>ios-simulator-64_13.5</Targets>
  </XHarnessAppFolderToTest>
</ItemGroup>
```

The `<Targets>` metadata is a required configuration that tells XHarness which kind of device/Simulator to target.
Use the XHarness CLI help command to find more (see the `--targets` option).

You can also specify some metadata that will help you configure the run better:

```xml
<ItemGroup>
  <XHarnessAppFolderToTest Include=".\appbundles\Contoso.Example.Tests.app">
    <!-- Timeout for the overall run of the whole Helix work item (including Simulator booting, app installation..) -->
    <WorkItemTimeout>00:20:00</WorkItemTimeout>

    <!-- Timeout for the actual test run (when TestRunner starts execution of tests) -->
    <!-- Should be smaller than WorkItemTimeout by several minutes -->
    <TestTimeout>00:12:00</TestTimeout>
  </XHarnessAppFolderToTest>
</ItemGroup>
```

Furthermore, you can configure the execution further:

```xml
<PropertyGroup>
  <!-- Optional: Specific version of Xcode to use -->
  <XHarnessXcodeVersion>11.4</XHarnessXcodeVersion>
</PropertyGroup>
```

### Android .apk payloads

To execute .apks, declare the `XHarnessPackageToTest` items:

```xml
<ItemGroup>
  <XHarnessPackageToTest Include="$(TestArchiveTestsRoot)apk\x64\System.Numerics.Vectors.Tests.apk">
    <!-- Package name: this comes from metadata inside the apk itself -->
    <AndroidPackageName>net.dot.System.Numerics.Vectors.Tests</AndroidPackageName>

    <!-- If there are > 1 instrumentation class inside the package, we need to know the name of which to use -->
    <AndroidInstrumentationName>net.dot.MonoRunner</AndroidInstrumentationName>
  </XHarnessPackageToTest>
</ItemGroup>
```

You can also specify some metadata that will help you configure the run better:

```xml
<ItemGroup>
  <XHarnessAppFolderToTest Include="$(TestArchiveTestsRoot)**\*.apk">
    <!-- Timeout for the overall run of the whole Helix work item (including Simulator booting, app installation..) -->
    <WorkItemTimeout>00:20:00</WorkItemTimeout>

    <!-- Timeout for the actual test run (when TestRunner starts execution of tests) -->
    <!-- Should be smaller than WorkItemTimeout by several minutes -->
    <TestTimeout>00:12:00</TestTimeout>
  </XHarnessAppFolderToTest>
</ItemGroup>
```

### WASM payloads

We currently do not support execution of WASM workloads directly, please refer to [XHarness tool pre-installation only](#xharness-tool-pre-installation-only) and call the `xharness wasm test` command manually.

### XHarness tool pre-installation only

In case you decide to request the SDK to pre-install the XHarness tool only without any specific payload, you can do so by setting the `IncludeXHarnessCli` property to `true`:

```xml
<PropertyGroup>
  <IncludeXHarnessCli>true</IncludeXHarnessCli>
  <MicrosoftDotNetXHarnessCLIVersion>1.0.0-prerelease.20322.1</MicrosoftDotNetXHarnessCLIVersion>
</PropertyGroup>
```

There will be an environmental variable set called `XHARNESS_CLI_PATH` set that will point to the XHarness CLI DLL that needs to be run:

```xml
<ItemGroup>
  <HelixWorkItem Include="Run WASM tests">
    <!-- %XHARNESS_CLI_PATH% for Windows which can be derived from $(IsPosixShell) property -->
    <Command>dotnet exec $XHARNESS_CLI_PATH wasm test --engine ...</Command>
  </HelixWorkItem>
</ItemGroup>
```
