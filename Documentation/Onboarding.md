# Onboarding onto Arcade

- Onboard onto the Arcade SDK, which provides templates (building blocks) for
  interacting with Azure DevOps, as well as shared tooling for signing,
  packaging, publishing and general build infrastructure.  
  
  Resources: [Reference documentation](ArcadeSdk.md), [walkthough video](https://msit.microsoftstream.com/video/e22d2dad-ef72-4cca-9b62-7e33621f86a1), [feature documentation](CorePackages/).

   Steps:
    1. Add a
       [global.json](https://github.com/dotnet/arcade-minimalci-sample/blob/master/global.json).
    2. Add (or copy)
       [Directory.Build.props](https://github.com/dotnet/arcade-minimalci-sample/blob/master/Directory.Build.props)
       and
       [Directory.build.targets](https://github.com/dotnet/arcade-minimalci-sample/blob/master/Directory.Build.targets).
    3. Copy `eng\common` from
       [Arcade](https://github.com/dotnet/arcade-minimalci-sample/tree/master/eng/common)
       into repo.
    4. Add (or copy) the
       [Versions.props](https://github.com/dotnet/arcade-minimalci-sample/blob/master/eng/Versions.props)
       and
       [Version.Details.xml](https://github.com/dotnet/arcade-minimalci-sample/blob/master/eng/Version.Details.xml)
       files to your eng\ folder. Adjust the version prefix and prerelease label
       as necessary.
    5. Add dotnet-core feed and any other feeds that the repository restores NuGet packages from to
       [NuGet.config](https://github.com/dotnet/arcade-minimalci-sample/blob/master/NuGet.config).
    6. Must have a root project/solution file for the repo to build.
    7. Additional package feeds can be added to the `eng\Version.props` file, e.g.
       ```
       <PropertyGroup>
         <RestoreSources>
           $(RestoreSources);
           https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json;
           https://dotnet.myget.org/F/symreader-converter/api/v3/index.json;
           https://dotnet.myget.org/F/symreader/api/v3/index.json
         </RestoreSources>
       </PropertyGroup>
       ```

    **Using Arcade packages** - See [documentation](CorePackages/) for
    information on specific packages.

- Move out of .NET CI and into our new Azure DevOps project
  (https://dev.azure.com/dnceng/public) for your public CI. - See [Onboarding
  Azure DevOps](AzureDevOps/AzureDevOpsOnboarding.md).
- Move out of the devdiv Azure DevOps instance (https://dev.azure.com/devdiv/ or
  https://devdiv.visualstudio.com) and into the internal project for
  (https://dev.azure.com/dnceng/internal) internal CI and official builds. - See
  [Onboarding Azure DevOps](AzureDevOps/AzureDevOpsOnboarding.md) and [Moving Official Builds from
  DevDiv to DncEng](AzureDevOps/MovingFromDevDivToDncEng.md).
- Onboard onto dependency flow (Darc). - See [Dependency Flow
  Onboarding](DependencyFlowOnboarding.md).
- Use Helix for testing where possible - See [Sending Jobs to Helix](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/SendingJobsToHelix.md)

## Which branches should I make these changes in?

Prioritize branches that are producing bits for .NET Core 3.  Given the extended
support lifecycle for .NET Core 2.1, backporting infrastructure to .NET Core 2.1
release branches is desired, but .NET Core 3 branches should go first.
