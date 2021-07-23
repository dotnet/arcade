# Validating Product Builds

Validating builds is an important part of producing a releasable product. There are several levels of validation that are performed on product builds at various phases of the process:

* Repo level functionality testing: this is controlled by the product teams and defined in the repositories, and is performed during PR and CI builds.
* Source code validtion: this includes SDL testing, as well as localization testing, and occurs either in the official build or in the post-build nightly Validation pipeline.
* Signing validation: validates that binaries have been signed and is performed post-build, post-signing, in the Validation pipeline.
* Package validation: includes sourcelink validation, symbols validation, nupkg metadata validation, and can be performed either in the official build or as part of the nightly Validation pipeline

While many of these validation steps can be performed in official builds, the supported model is to onboard to the nightly Validation pipeline.

## How do we Validate DotNet (and the product repos)?

Pre-.NET 5, validation for the the core dotnet repositories were done during the official builds. For .NET 5, all post-build validation and source code validation was moved out of the official build and into a separate nightly pipeline, [Validate-DotNet](https://dev.azure.com/dnceng/internal/_build?definitionId=827). This pipeline runs all the same validation that is performed on the full product when we go to release, and is a smoke test for product teams, so that product teams can address any issues well before release day.

Validate-DotNet is controlled by a separate scheduling pipeline, which looks for the newest build for each repository that has been on-boarded on every channel that those repositories publish to. If no build is found from the previous 24 hours, no validation run will be started for that channel/repo combination. For any respository that has published a new build to the BAR database for a given channel, a new Validate-DotNet run will be created.

Validate-DotNet uses the information in the BAR database, and in particular, the `gather-drop` command to pull down all of the assets that were created in a particular build (but no downstream assets). The pipeline will then sign the build before running the various validation checks on that build. Validation includes:

* Signing Validation
* SDL Validation (which will open TSA issues for any failures found)
* Localization Validation
* Nuget Metadata Validation
* Sourcelink Validation
* Symbols Validation
* Nuget Package Icon Validation
* Checksums Validation

Please note, we are always adding new validation to the pipeline, so this is not an exhaustive list.

## Why move validation out of official builds

As a part of .NET 5, we had a goal of 2 hour build turn-around. In order to close in on that goal, we removed many things out of official builds including post-build validation.

## Onboarding to Validate-DotNet

[Validate-DotNet](https://dev.azure.com/dnceng/internal/_build?definitionId=827) is a pipeline that automatically runs nightly validation for all repositories that have been onboarding. Onboarding to Validate-DotNet is quite simple:

1. Update the [list of repos](https://dev.azure.com/dnceng/internal/_git/dotnet-release?path=%2Feng%2Fpipeline%2Ftools%2Frepos-to-validate.txt) in [dotnet-release](https://dev.azure.com/dnceng/internal/_git/dotnet-release) with the url to your repo. Please reach out to dnceng for PR approval.
2. To enable nightly SDL runs, add the [sdl-tsa-vars.config](https://github.com/dotnet/runtime/blob/main/eng/sdl-tsa-vars.config) file to your repo. This file should include all of the necessary information specific to your repository for creating SDL issues.

Once the first step is complete, your repository will start validating on a nightly basis. Once the second step is complete, you will also get SDL validation.

## How do I know if there are failures in my validation runs?

There are two main ways of checking your validation results: manual, and using automatic notifactions.

### Manual looking at the pipeline

Validate-DotNet runs each day at 4pm pacific time. Each run is tagged with the repo name and the channel of the given run (to distinguish between the various versions). To look at the runs for only your repository, you can use the following url, with your repository name in place of `<repo>` `https://dev.azure.com/dnceng/internal/_build?definitionId=827&tagFilter=<repo>`. For example, if you wanted to look at results for runtime, you would navigate to `https://dev.azure.com/dnceng/internal/_build?definitionId=827&tagFilter=runtime`.

### Automatic Notifications

Notifcations use build monitor to automatically monitor builds of Validate-DotNet for a particular repository and open issues when failures occur. If an issue is already open for Validate-DotNet failures in your repo, it will append a new comment.

To onboard, you will need to update both the `Builds` array and the `Issues` array in the [settings.json](https://github.com/dotnet/arcade-services/blob/main/src/DotNet.Status.Web/.config/settings.json#L23).

The `Builds` array controls which Azure DevOps pipeline builds will be monitored. To monitor Validate-DotNet runs for your repo, you will need to add a new item with the following information:

```json
{
    "Project": "internal",
    "DefinitionPath": "\\dotnet-release\\Validate-DotNet",
    "Branches": [ "main" ],
    "IssuesId": <id that matches an item in the Issues array>,
    "Tags": [ <repository name> ]
}
```

For example, for runtime, we would add

```json
{
    "Project": "internal",
    "DefinitionPath": "\\dotnet-release\\Validate-DotNet",
    "Branches": [ "main" ],
    "IssuesId": "dotnet-runtime-infra",
    "Tags": [ "runtime" ]
}
```

The `Issues` array controls where issues will be opened for builds that fail. You will need update the `Issues` array in the same settings.json file, with the following information:

```json
{
    "Id": <id that matches the IssuesId in your Builds entry>,
    "Owner": <project>, 
    "Name": <repository>, 
    "Labels": [ <infrastructure label> ],
    "UpdateExisting": true // True if you want issues to be updated. False if you want new issues for every failure
}
```

For example, for runtime, we would do:

```json
{
    "Id": "dotnet-runtime-infra",
    "Owner": "dotnet",
    "Name": "runtime",
    "Labels": [ "area-Infrastructure" ],
    "UpdateExisting": true
}
```
