# Sending Jobs to Helix

The primary reason to use Helix is to leverage its scalability to run tests. Arcade and the Arcade SDK provide out-of-the-box functionality to interface with Helix.

## Getting Started

First, you have to import the SDK. Everything that follows requires dotnet-cli ≥ 2.1.300 and needs the following files in a directory at or above the project's directory.

#### global.json
```json
{
  "msbuild-sdks": {
    "Microsoft.DotNet.Helix.Sdk": "<version of helix sdk package from package feed>"
  }
}
```

Example: `"Microsoft.DotNet.Helix.Sdk": "1.0.0-beta.18502.3"`
#### NuGet.config
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

If you plan to send a payload (such as a work item) to Helix, you will need to be authorized. For official builds you can use your current access token; however, sending jobs from public CI builds is a different story.

For these builds, you will need to add a variable group to your build which contains only the *BotAccount-dotnet-github-anon-kaonashi-bot-helix-token* secret from EngKeyVault. This will allow you to send telemetry to Helix while minimizing the risk of leaking secrets.

## The Simple Case

The simplest Helix use-case is zipping up a single folder containing your project's tests and a batch file which runs those tests. To accomplish this, reference Arcade's `helix-publish` template in `eng/common/templates/steps/helix-publish.yml` from your `.vsts-ci.yml` file.

In a case where you have a directory called `tests` and a batch file in that directory called `runtests.cmd` which will run several different XUnit test projects:

```yaml
  - template: /eng/common/templates/steps/helix-publish.yml
    parameters:
      HelixSource: your/helix/source
      HelixType: type/tests
      # HelixBuild: $(Build.BuildNumber) -- This property is set by default
      HelixTargetQueues: Windows.10.Amd64.Open;Windows.7.Amd64.Open
      HelixAccessToken: $('BotAccount-dotnet-github-anon-kaonashi-bot-helix-token')
      # HelixPreCommands: '' -- any commands that you would like to run prior to running your job
      # HelixPostCommands: '' -- any commands that you would like to run after running your job
      WorkItemDirectory: $(Build.SourcesDirectory)/artifacts/bin/test/$(_BuildConfig)/netcoreapp2.0
      WorkItemCommand: runtests.cmd
      EnableXUnitReporter: true # required for reporting out XUnit test results
      # WaitForWorkItemCompletion: true -- defaults to true
```

## The More Complex Case

For anything more complex than the above example, you'll want to create your own MSBuild proj file to specify the work items and correlation payloads you want to send up to Helix. Full documentation on how to do this can be found [in the SDK's readme](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Helix/Sdk/Readme.md).
