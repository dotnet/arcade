# Dotnet.visualstudio.com Internal mirror

Public code should be mirrored to dotnet.visualstudio.com/internal (see [VSTS Guidance](https://github.com/dotnet/arcade/blob/master/Documentation/VSTS/VSTSGuidance.md)).  These are the steps for setting up your GitHub repo to mirror into dotnet.visualstudio.com/internal.

1. Add the [maestro web hook](https://github.com/dotnet/core-eng/blob/master/Documentation/Maestro/web-hooks.md)
2. Make sure you have created a repo in the dotnet.visualstudio.com/internal project that is in the format "{org}-{repo}" (replace  any `/` with `-` in the github repo name).
    - Example: github.com/dotnet/Arcade => dotnet-arcade
3. Add data for repo and branches that you want mirrored to the [subscriptions json](https://github.com/dotnet/versions/blob/master/Maestro/subscriptions.json#L542)