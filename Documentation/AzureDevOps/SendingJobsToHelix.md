# Sending Jobs to Helix

The primary reason to use Helix is to leverage its scalability to run tests. Arcade and the Arcade SDK provide out-of-the-box functionality to interface with Helix.

## Getting Started

First, you have to import the SDK. Everything that follows requires dotnet-cli ≥ 2.1.300 and needs the following files in a directory at or above the project's directory.

### global.json

```json
{
  "msbuild-sdks": {
    "Microsoft.DotNet.Helix.Sdk": "<version of helix sdk package from package feed>"
  }
}
```

Example: `"Microsoft.DotNet.Helix.Sdk": "1.0.0-beta.18502.3"`

### NuGet.config

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
  </packageSources>
</configuration>
```

## Helix Access Token

Helix access tokens are used to authenticate users sending jobs to Helix.

### External builds (Public CI)

For external builds, you don't need to specify an access token (indeed, doing so is prohibited). Simply specify a *Creator* in order to send the jobs to Helix anonymously. The "creator" should be an identifiable username which is clearly related to your build. For example, Arcade might specify a creator of `arcade`. This will make collating your results on Mission Control much easier.

Please note that external jobs may only be submitted to queues which end with the value `IsInternalOnly` set to false. In general, these queues end with **.Open**; however, this is not necessarily true.  To determine this value for a particular queue, see the list of available queues [here](https://helix.dot.net/api/2018-03-14/info/queues).

Example:

```yaml
steps:
- template: /eng/common/templates/steps/send-to-helix.yml
  displayName: Send to Helix
  parameters:
    ## more variables go here
    Creator: # specify your creator here
```

### Internal builds

In the dev.azure.com/dnceng/internal project, you can use the `DotNet-HelixApi-Access` variable group to provide this secret to your build and then specify the `HelixApiAccessToken` secret for the `HelixAccessToken` parameter.

Please note that authorized jobs *cannot* be submitted to queues with `IsInternalOnly` set to false. To determine this value for a particular queue, see the list of available queues [here](https://helix.dot.net/api/2018-03-14/info/queues).

Example:

```yaml
variables:
- group: DotNet-HelixApi-Access

steps:
# $HelixAccessToken is automatically injected into the environment
- template: /eng/common/templates/steps/send-to-helix.yml
  displayName: Send to Helix
  parameters:
    HelixAccessToken: $(HelixApiAccessToken)
    # other parameters here
```

## The Simple Case

The simplest Helix use-case is zipping up a single folder containing your project's tests and a batch file which runs those tests. To accomplish this, reference Arcade's `send-to-helix` template in `eng/common/templates/steps/send-to-helix.yml` from your `azure-pipelines.yml` file.

Simply specify the xUnit project(s) you wish to run (semicolon delimited) with the `XUnitProjects` parameter. Then, specify:
* the `XUnitPublishTargetFramework` &ndash; this is the framework your **test projects are targeting**, e.g. `netcoreapp2.1`.
* the `XUnitRuntimeTargetFramework` &ndash; this is the framework version of xUnit you want to use from the xUnit NuGet package, e.g. `netcoreapp2.0`. Notably, the xUnit console runner only supports up to netcoreapp2.0 as of 14 March 2018, so this is the target that should be specified for running against any higher version test projects.
* the `XUnitRunnerVersion` (the version of the xUnit nuget package you want to use, e.g. `2.4.1`).

Finally, set `IncludeDotNetCli` to true and specify which `DotNetCliPackageType` (`sdk` or `runtime`) and `DotNetCliVersion` you wish to use. (For a full list of .NET CLI versions/package types, see these links: [3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0), [2.1](https://dotnet.microsoft.com/download/dotnet-core/2.1), [2.2](https://dotnet.microsoft.com/download/dotnet-core/2.2).)

The list of available Helix queues can be found on the [Helix homepage](https://helix.dot.net/).

```yaml
  - template: /eng/common/templates/steps/send-to-helix.yml
    parameters:
      HelixSource: pr/your/helix/source # sources must start with pr/, official/, prodcon/, or agent/
      HelixType: type/tests
      # HelixBuild: $(Build.BuildNumber) -- This property is set by default
      HelixTargetQueues: Windows.10.Amd64.Open;Windows.81.Amd64.Open;Windows.7.Amd64.Open # specify appropriate queues here; see https://helix.dot.net/ for a list of queues
      # HelixAccessToken: $(HelixAccessToken) this token is only for internal builds
      # HelixPreCommands: '' -- any commands that you would like to run prior to running your job
      # HelixPostCommands: '' -- any commands that you would like to run after running your job
      XUnitProjects: $(Build.SourcesDirectory)/HelloTests/HelloTests.csproj # specify your xUnit projects (semicolon delimited) here!
      # XUnitWorkItemTimeout: '00:05:00' -- a timeout (specified as a System.TimeSpan string) for all work items created from XUnitProjects
      XUnitPublishTargetFramework: netcoreapp2.1 # specify your publish target framework here
      XUnitRuntimeTargetFramework: netcoreapp2.0 # specify the framework you want to use for the xUnit runner
      XUnitRunnerVersion: 2.4.1 # specify the version of xUnit runner you wish to use here
      # WorkItemDirectory: '' -- payload directory to zip up and send to Helix; requires WorkItemCommand; incompatible with XUnitProjects
      # WorkItemCommand: '' -- a command to execute on the payload; requires WorkItemDirectory; incompatible with XUnitProjects
      # WorkItemTimeout: '' -- a timeout (specified as a System.TimeSpan string) for the work item command; requires WorkItemDirectory; incompatible with XUnitProjects
      IncludeDotNetCli: true
      DotNetCliPackageType: sdk
      DotNetCliVersion: 2.1.403 # full list of versions here: https://raw.githubusercontent.com/dotnet/core/master/release-notes/releases.json
      EnableXUnitReporter: true # required for reporting out xUnit test results to Mission Control
      # WaitForWorkItemCompletion: true -- defaults to true
      Creator: arcade # specify an appropriate Creator here -- required for external builds
      # DisplayNamePrefix: 'Send job to Helix' -- the Helix task's display name in AzDO. Defaults to 'Send job to Helix'
      # condition: succeeded() - defaults to succeeded()
```

## The More Complex Case

For anything more complex than the above example, you'll want to create your own MSBuild proj file to specify the work items and correlation payloads you want to send up to Helix. Full documentation on how to do this can be found [in the SDK's readme](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/Readme.md).

## Viewing test results

All test results will be downloaded to the Azure DevOps build and viewable through the **Tests** tab. However, you can also view the results on Mission Control.

### External test results

Tests results for "public" projects are accessible via the link which is provided in the build output.

Example build output:

```Text
Sent Helix Job 7a6bb019-ed0e-4e46-a065-a38391d90019
Waiting on job completion...
Results will be available from https://mc.dot.net/#/user/dotnet-github-anon-kaonashi-bot/pr~2Fdotnet~2Fwinforms~2Frefs~2Fpull~2F9~2Fmerge/type~2Ftests/20181029.7
```

### Internal test results

Test results for "internal" projects are accessible via the link which is provided in the build output or via configurable [Mission Control views](https://github.com/dotnet/core-eng/blob/ad1d9dd5b9797f0e659a647dbce9e8c842fa3324/Documentation/HelixDocumentation.md#mission-control).
