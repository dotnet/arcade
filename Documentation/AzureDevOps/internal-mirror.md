# dev.azure.com/dnceng Internal Mirror

Public code should be mirrored to dev.azure.com/dnceng/internal (see [Azure DevOps Guidance](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/AzureDevOpsGuidance.md)).  These are the steps for setting up your GitHub repo to mirror into dev.azure.com/dnceng/internal.

1. Add the [maestro web hook](https://github.com/dotnet/core-eng/blob/master/Documentation/Maestro/web-hooks.md)
2. Make sure you have created a repo in the dev.azure.com/dnceng/internal project that is in the format "{org}-{repo}" (replace  any `/` with `-` in the GitHub repo name).
    - Example: github.com/dotnet/arcade => dotnet-arcade
3. Add data for repo and branches that you want mirrored to the [subscriptions json](https://github.com/dotnet/versions/blob/master/Maestro/subscriptions.json). Specifically, add a URI of the pattern `"https://github.com/{org}/{repo}/blob/{branch}/**/*"` for your GitHub repository to the `triggerPaths` list above `"action": "github-dnceng-azdo-mirror"`. Please alphabetize.

```
        "https://github.com/dotnet/project-system/blob/release/**/*",
        "https://github.com/dotnet/toolset/blob/master/**/*",
        "https://github.com/dotnet/toolset/blob/release/**/*",
        "https://github.com/dotnet/roslyn/blob/master/**/*",
        "https://github.com/dotnet/roslyn/blob/release/**/*",
        "https://github.com/{org}/{repo}/blob/{branch}/**/*" // <-- insert your URI here, in alpha order.
        "https://github.com/Microsoft/msbuild/blob/master/**/*",
        "https://github.com/Microsoft/msbuild/blob/release/**/*",
      ],
      "action": "github-dnceng-azdo-mirror",
      "actionArguments": {
"vsoSourceBranch": "master",
```
