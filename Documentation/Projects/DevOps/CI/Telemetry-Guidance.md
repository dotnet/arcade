# Telemetry Guidance

- [Overview](#overview)
- [Telemetry format](#telemetry-format)
- [Writing to the Timeline API](#writing-to-the-timeline-api)
- [Arcade support for categorized telemetry](#arcade-support-for-writing-categorized-telemetry)

## Overview

Arcade projects will emit telemetry metadata that allows us to more easily classify where failures occur.  This metadata will follow a prescribed format outlined below.  The metadata will be written to Azure DevOp's [Timeline API](https://docs.microsoft.com/en-us/rest/api/azure/devops/build/Timeline/Get?view=azure-devops-rest-5.0) so that it can be gathered later and moved to our Engineering database or otherwise analyzed.

## Telemetry format

`(NETCORE_ENGINEERING_TELEMETRY=[telemetry category]) [Message]`

### Example

`(NETCORE_ENGINEERING_TELEMETRY=Publish) Publishing failed with exit code 1.`

## Writing to the Timeline API

Data can be written into Azure DevOp's [Timeline API](https://docs.microsoft.com/en-us/rest/api/azure/devops/build/Timeline/Get?view=azure-devops-rest-5.0) by writing error or warning messages to the console in the prescribed [Azure DevOps format](https://github.com/Microsoft/azure-pipelines-tasks/blob/master/docs/authoring/commands.md).

### Example

`echo "##vso[task.logissue type=error](NETCORE_ENGINEERING_TELEMETRY=Publish) Publishing failed with exit code 1."`

## Arcade support for writing categorized telemetry

Arcade will provide support for writing telemetry both in scripts (Powershell and bash) and in [Arcade's MSBuild logger](https://github.com/dotnet/arcade/blob/3079c495f38bb1306a65b2af13cf25a516610a4f/src/Microsoft.DotNet.Arcade.Sdk/src/PipelinesLogger.cs).

### Arcade script support

Arcade scripts will support a `Write-PipelineTelemetryError` function that can be called and will provide properly formatted error message output.

Repos with custom scripts can add telemetry categorization by using Arcade's logging functions which are available in their repo via dependency flow of the `eng/common` scripts.

### Arcade MSBuild logger support

Arcade's MSBuild logger will be modified to look for an `NETCORE_ENGINEERING_TELEMETRY` property.  If present, then error output will be decorated in the expected [telemetry format](#telemetry-format).

This will allow us to add telemetry properties into MSBuild targets which will then decorate the Timeline API results with the expected categorization.  

Repos with custom MSBuild targets that are using Arcade's pipeline logger, will get similar functionality by adding the "NETCORE_ENGINEERING_TELEMETRY" property group or defining an environment variable with their desired categorization (MSBuild automatically turns environment variables into MSBuild properties).

#### Example

If we add...

```XML
<PropertyGroup>
  <NETCORE_ENGINEERING_TELEMETRY>Signing</NETCORE_ENGINEERING_TELEMETRY>
</PropertyGroup>
```

into Arcade's [Sign.proj](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Arcade.Sdk/tools/Sign.proj) file, then any CI failures in Signing targets will be properly tagged by Arcade's logger.  This will enable categorization for anyone using Arcade.

## Logging categories

We are not intending to be proscriptive are hard-lined about a specific set of categories that a repo must use when sending telemetry.  Initially, however, we should look to modify Arcade to categorize "Restore", "Build", "Test", "Sign", and "InitializeToolset" (Arcade script) changes.

### Example chart

This is a completely made up example of a possible way we could surface this data.

[Category chart](./Category-sample.png)

Repos may provide their own categories or use existing reports as examples of good category names.
