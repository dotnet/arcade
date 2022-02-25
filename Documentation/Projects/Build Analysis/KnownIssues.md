# Known issues
The "known issues" are GitHub  issues that were created with the purpose of report known build errors. 

One of the primary features of known issues is that when a build has an error that matches with the reported error this is going to be listed on the [build analysis](https://github.com/dotnet/arcade/blob/main/Documentation/Projects/Build%20Analysis/Introduction.md), helping the developer to know when a build break is not exclusive to their build. 
This information is going to be listed at the top of the build analysis.


If you need to find the open known issues you can filter the issues with the custom label `Known Build Error`. All the reported infrastructure issues can be found on [core-eng](https://github.com/dotnet/core-eng/issues?q=is%3Aopen+is%3Aissue+label%3A%22Known+Build+Error%22).

## When and how to report a known issue
A known issue should be reported when you find a build error that is not caused by your changes and that is affecting or could affect more builds. 

There are two types of known issues:
- Infrastructure: An infrastructure issue is an issue that is not exclusive to your repository and that needs to be investigated by the engineering services (@dotnet/dnceng)
- Repository: A repository issue is an issue that is happening in a particular repository and that should be investigated by the repository owners.

The first step to opening a known issue is to decide which kind of issue do you want to report, after that there are two ways to report a known issue, one is via the build analysis.

### Reporting known issue manually
1. Decide if you need to open a repository issue or infrastructure issue 
1. Open a new issue, choosing the repository in which you are opening the issue based on following rule:
    - Infrastructure issue - core-eng
    - Repository issue - In the repo in which the issue is happening
1. Add the label `Known Build Error`. (If the label is not available on the repository follow the instructions to [get on board](#how-to-get-onboard))

### Reporting known issue via build analysis
1. On the build analysis you will see links for the type of issue you want to open. 
![](./Resources/KnownIssuesLinks.png?raw=true)
1. Click on the link of the type of issue that suits better the situation. 

If you are filing it manually, copy and paste the next template. If you are filing via the build analysis, a similar template will be automatically added on a new GitHub issue after clicking the link, and most of this information should be already prefilled.

```md 
## Build Information
Build: <!-- Add link to the build with the reported error. -->
Leg Name: <!-- Add the name of the impacted leg. -->

## Error Message
```json 
{ "ErrorMessage":"" } ```

```

If you are opening an infrastructure issue, there is no need to fill the  "Error message section" as this is going to be handled by the engineering services team.

If you are opening a Repository issue you need to [fill the "Error message section"](#how-to-fill-a-known-issue-error-message-section")


## How to fill a known issue error message section
The "ErrorMessage section" is on the next form:
```json 
{ "ErrorMessage":"" } 

```

And the "ErrorMessage" value needs to be updated with an error message 
that meets the following requeriments:
1. Match at least one of the error messages of the reported build
2. It shouldn't include any unique identifier of the message e.g., machine, path, file name, etc.


    For example, for the following error:

    ```log
    ##[error].dotnet/sdk/6.0.100-rc.1.21411.28/NuGet.RestoreEx.targets(19,5): error : (NETCORE_ENGINEERING_TELEMETRY=Restore) Failed to retrieve information about 'Microsoft.Extensions.Hosting.WindowsServices' from remote source 'https://pkgs.dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_packaging/c9f8ac11-6bd8-4926-8306-f075241547f7/nuget/v3/flat2/microsoft.extensions.hosting.windowsservices/index.json'.
    ```

    We could choose this part of the message: `Failed to retrieve information about 'Microsoft.Extensions.Hosting.WindowsServices' from remote source`. This is a good option because it targets a particular problem but excludes details of the build, thus making it possible for this same error to match with other builds. 

After selecting the message, fill the "ErrorMessage";

```json 
{ 
    "ErrorMessage":"Failed to retrieve information about 'Microsoft.Extensions.Hosting.WindowsServices'" 
}
```

## How to get onboard
The infrasctucture issues are part of dotnet/core-eng so there are not additional steps that needs to be taken care of. 

For the repository issues it it's necessary to [Create a label](https://docs.github.com/en/enterprise-server@3.1/issues/using-labels-and-milestones-to-track-work/managing-labels#creating-a-label) on the repository in which you need to open the Repository issue. 

The name of the label needs to be `Known Build Error`


## Where to find the known issue on the build analysis
The known issues are listed at the top of the build analysis. <br>
E.g.
![](./Resources/KnownIssuesListed.png?raw=true)
