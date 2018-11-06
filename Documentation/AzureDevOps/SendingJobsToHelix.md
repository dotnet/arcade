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

If you plan to send a payload (such as a work item) to Helix, you will need to be authorized. 

### External builds (Public CI)

For external builds, you will need to provide the *BotAccount-dotnet-github-anon-kaonashi-bot-helix-token* secret from **ToolsKV** (subscription *Dotnet Engineering services*). This will allow you to send payloads to Helix while minimizing the risk of leaking secrets.

You can either copy this secret into your build definition as a variable and mark it as secret, or; in the dev.azure.com/dnceng/public project, you can use the `Helix Anonymous` variable group to provide this secret to your build

### Internal builds

In the dev.azure.com/dnceng/internal project, you can use the `DotNet-HelixApi-Access` variable group to provide this secret to your build.

## The Simple Case

The simplest Helix use-case is zipping up a single folder containing your project's tests and a batch file which runs those tests. To accomplish this, reference Arcade's `helix-publish` template in `eng/common/templates/steps/helix-publish.yml` from your `.Azure DevOps-ci.yml` file.

You will need to create a script file to run your tests. In the future, it will be possible to simply specify the directory where your xUnit tests live and the job sender will intelligently handle the rest of this for you; currently, however, this functionality does not exist.

```yaml
  - template: /eng/common/templates/steps/helix-publish.yml
    parameters:
      HelixSource: pr/your/helix/source # sources must start with pr/, official/, prodcon/, or agent/
      HelixType: type/tests
      # HelixBuild: $(Build.BuildNumber) -- This property is set by default
      HelixTargetQueues: Windows.10.Amd64.Open;Windows.7.Amd64.Open
      HelixAccessToken: $(BotAccount-dotnet-github-anon-kaonashi-bot-helix-token)
      # HelixPreCommands: '' -- any commands that you would like to run prior to running your job
      # HelixPostCommands: '' -- any commands that you would like to run after running your job
      WorkItemDirectory: $(Build.SourcesDirectory)/artifacts/bin/test/$(_BuildConfig)/netcoreapp2.0 # specify the directory where your tests live here
      WorkItemCommand: # specify the command to run your tests
      EnableXUnitReporter: true # required for reporting out xUnit test results to Mission Control
      # WaitForWorkItemCompletion: true -- defaults to true
      # condition: succeeded() - defaults to succeeded()
      # continueOnError: false -- defaults to false
```

## The More Complex Case

For anything more complex than the above example, you'll want to create your own MSBuild proj file to specify the work items and correlation payloads you want to send up to Helix. Full documentation on how to do this can be found [in the SDK's readme](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/Readme.md).

## Viewing test results

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
