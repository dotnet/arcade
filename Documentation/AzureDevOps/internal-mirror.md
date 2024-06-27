# dev.azure.com/dnceng Internal Mirror

Public code should be mirrored to dev.azure.com/dnceng/internal (see [Azure DevOps Guidance](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/AzureDevOpsGuidance.md)).  These are the steps for setting up your GitHub repo to mirror into dev.azure.com/dnceng/internal.

1. Add the [maestro web hook](https://github.com/dotnet/arcade/blob/main/Documentation/Maestro/web-hooks.md)
2. Make sure you have created a repo in the dev.azure.com/dnceng/internal project that is in the format "{org}-{repo}" (replace  any `/` with `-` in the GitHub repo name).
    - Example: github.com/dotnet/arcade => dotnet-arcade
3. Create a PR to the `dotnet-mirroring` internal repo which adds data for repo and branches that you want mirrored to the [subscriptions json](https://dev.azure.com/dnceng/internal/_git/dotnet-mirroring?path=/dnceng-subscriptions.jsonc) file. Specifically, add a URI for your GitHub repository to the `repos` object. Please alphabetize.

Example:
```json
    "https://github.com/dotnet/source-indexer": {
      "fastForward": [
        "main"
      ]
    },
    "https://github.com/dotnet/sourcelink": {
      "fastForward": [
        "main",
        "release/.*"
      ]
    },
    "https://github.com/dotnet/spa-templates": {
      "fastForward": [
        // GitHubBranchNotFound "main",
        "release/.*"
      ]
    },
```


<!-- Begin Generated Content: Doc Feedback -->
<sub>Was this helpful? [![Yes](https://helix.dot.net/f/ip/5?p=Documentation%5CAzureDevOps%5Cinternal-mirror.md)](https://helix.dot.net/f/p/5?p=Documentation%5CAzureDevOps%5Cinternal-mirror.md) [![No](https://helix.dot.net/f/in)](https://helix.dot.net/f/n/5?p=Documentation%5CAzureDevOps%5Cinternal-mirror.md)</sub>
<!-- End Generated Content-->
