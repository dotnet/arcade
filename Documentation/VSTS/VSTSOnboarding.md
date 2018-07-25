# Onboarding VSTS

- [Project Guidance](#project-guidance)
- [GitHub to DotNet Internal mirror](#github-to-dotnet-internal-mirror)
- [VSTS Pull Request and CI builds](#vsts-pull-request-and-ci-builds)
- [Agent Queues](#agent-queues)
- [VSTS GitHub connection](#vsts-github-connection)
- [CI badge link](#ci-badge-link)
- [Signed builds](#signed-builds)
- [Security](#security)
- [Notes about YAML](#notes-about-yaml)
- [Troubleshooting](#troubleshooting)

## Project Guidance

[Project guidance](./VSTSGuidance.md) - Covers guidance on naming conventions, folder structure, projects, build definitions, etc...

## GitHub to DotNet Internal mirror

If your repository has internal builds, you will need to set up a DotNet Internal mirror. This is *required* for internal builds; if your repository only does PR or public CI builds, you can skip this step.

Instructions for setting up the GitHub to dotnet.visualstudio.com/internal mirror are available in the [Dotnet.visualstudio.com internal mirror documentation](./internal-mirror.md)

## VSTS Pull Request and CI builds

[VSTS Pull Request and CI builds](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts) - VSTS documentation for enabling VSTS public CI and Pull Request builds

## Agent queues

Agent queue use / configuration / etc... is likely to change very soon, at the moment, agent queues are primarily relegated to Hosted machine pools.  When VSTS enables "bring your own cloud", we'll provide greater flexibility / capacity of machines.

A couple of notes:

- Space on [Hosted machines](https://docs.microsoft.com/en-us/vsts/pipelines/agents/hosted?view=vsts#capabilities-and-limitations) is only guaranteed to be at least 10 GB.  We have a "Helix" machine pool which has greater disk space capacity.  This pool has machines running VMs with Windows Server 2016, MSBuild 15.0, and Visual Studio 2017 installed.  Connection instructions for connecting to these machines can be found in the [VSTS Windows Connection Instructions](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/vsts-windows-connection-instructions.md) document.

- For Linux, use the "DotNetCore-Linux" machine pool instead of "Hosted Linux Preview".  "Hosted Linux Preview" is not guaranteed to have docker installed.

## VSTS GitHub connection

VSTS will require a GitHub Service Endpoint to communicate with GitHub and setup web hooks.  Teams should use the `DotNet-Bot GitHub Connection` Service Endpoint.  The `DotNet-Bot GitHub Connection` requires that teams add the .NET Core owned [service account](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/dotnet-bot-github-service-endpoint.md#github-service-account) as a [collaborator](https://help.github.com/articles/permission-levels-for-a-user-account-repository/#collaborator-access-on-a-repository-owned-by-a-user-account) (Admin access) on the GitHub repo.

For implementation details and managing information about `DotNet-Bot GitHub Connection` see the [documentation](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/dotnet-bot-github-service-endpoint.md#vsts-service-endpoint)

## CI badge link

The [VSTS CI Build guidance](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts#create-a-vsts-build-status-with-a-github-readme-file) describes how to determine the CI build badge link, but only for task based build definitions.  If you're using a YAML based build definition, then you can determine the badge link by either of these two methods.

- Edit the build definition and go to the "History" tab.  Select the most recent change, right-click, and select "Compare Differences".  Scroll through the json and look for the "_links" section to find the "badge" link.

```JSON
"_links": {
  "self": {
      "href": "https://dotnet.visualstudio.com/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_apis/build/Definitions/15?revision=4"
  },
  "web": {
      "href": "https://dotnet.visualstudio.com/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_build/definition?definitionId=15"
  },
  "editor": {
      "href": "https://dotnet.visualstudio.com/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_build/designer?id=15&_a=edit-build-definition"
  },
  "badge": {
      "href": "https://dotnet.visualstudio.com/_apis/public/build/definitions/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/15/badge"
  }
},
```

- Use the method provided in the [CI Build Guidance](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts#create-a-vsts-build-status-with-a-github-readme-file) to discover the badge link for a task based build definition in the same project, then replace the build definition id with the build definition id of the yaml based build you are modifying.  The Url encoding follows this pattern...

  `https://[project collection]/_apis/[project name]/build/definitions/[project id]/[build definition id]/badge`

It is recommended that you restrict the CI build status to a particular branch.  This will prevent the badge from reporting Pull Request build status.  Restrict to a branch by adding the `branchName` parameter to your query string.

Example:

```Text
https://dotnet.visualstudio.com/DotNet-Public/_build/index?definitionId=17&branchName=master
```

## Signed Builds

Dotnet.visualstudio.com does not have support for signed builds.  Code should still be mirrored to dotnet.visualstudio.com/internal as outlined in the [VSTS Guidance](./VSTSGuidance.md#projects), but build definitions for signing should be created in devdiv.visualstudio.com, see the additional [signing documentation](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/signed-dotnet.visualstudio.com-builds.md)

## Security

[Security documentation](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts#security-considerations)

It is recommended that you do **NOT** enable the checkbox labeled "Make secrets available to builds of forks".

## Notes about Yaml

- Code reuse

  For *most* teams, it is recommended that you author your yaml to use the [same yaml files for internal, CI, and Pull Request builds](./WritingBuildDefinitions.md).  See https://github.com/dotnet/arcade/blob/master/eng/build.yml, for how this is being done in Arcade with build steps conditioned on "build reason".  Note that VSTS does not yet provide build reason in the template evaluation context, so it is currently explicitly provided via two entry points: [Internal builds entry point](https://github.com/dotnet/arcade/blob/master/.vsts-dotnet.yml#L5) and [CI / Pull Request builds entry point](https://github.com/dotnet/arcade/blob/master/.vsts-dotnet-ci.yml#L5)

  Expect build reason to be available for template evaluation by the end of June, 2018.  At that time, Arcade's `.vsts-dotnet.yml` and `.vsts-dotnet-ci.yml` files will be combined into a single file and the explicit "buildReason" variable will be removed.

- Shared templates

  Arcade currently provides a handful for [shared templates](https://github.com/dotnet/arcade/tree/master/eng/common/templates).  At the moment, it is only recommended that you add the `base` template to your yml phases ([example](https://github.com/dotnet/arcade/blob/master/eng/build.yml#L19)).  More templates will be provided when VSTS fixes some current bugs (see "Notes about templates")

  - [base.yml](https://github.com/dotnet/arcade/blob/master/eng/common/templates/phases/base.yml) defines docker variables, and enables telemetry to be sent for non-CI builds.

- Variable groups

  Variable groups are not yet supported in Yaml.  They are scheduled to be available soon (June 2018), in the interim if you need to access a key vault secret, you can explicitly reference a key vault secret using the VSTS key vault task.

Notes about templates:

- Additional info about templates - https://github.com/Microsoft/vsts-agent/blob/master/docs/preview/yamlgettingstarted-templates.md

- Currently, VSTS has a problem with multiple levels of templates where it will change the order of build steps you provide.  That means, for now, use templates judiciously, don't layer templates within templates, and always validate the step order via a CI build.

## Troubleshooting

### Known issues

For a list of known VSTS issues we are tracking, please go [here](https://dotnet.visualstudio.com/internal/_queries/query/7275f17c-c42f-44b8-9798-9c2426bf8395/)

### Queuing builds

- YAML: "The array must contain at least one element. Parameter name: phases"

  If your template doesn't compile, then it may prevent any of your "phase" elements from surfacing which leads to this error.  This error hides what the real error in the template is.  You can work around this error by providing a default phase.

  ```YAML
  phases:
  - phase: foo
    steps:
    - script: echo foo
  ```

  With a default phase provided, when you queue a build, VSTS will now tell you the actual error you care about.  VSTS is hotfixing this issue so that the lower-level issue is surfaced.

- "Resource not authorized" or "The service endpoint does not exist or has not been authorized for use"

  If you made some change to a resource or changed resources, but everything else *appears* correct (from a permissions / authorization point of view), and you're seeing "resource not authorized" when you try to queue a build; it's possible that the resource is fine, but the build definition is not authorized to use it.  Edit the build definition and make some minor change, then save.  This will force the build definition to re-authorize and you can undo whatever minor change you made.

  Note that [resource authorization](https://github.com/Microsoft/vsts-agent/blob/d792192875381ea770f09f3740ed8d1051f4f456/docs/preview/yamlgettingstarted-authz.md) happens on Push, not for Pull Requests.  If you have some changes to resources that you want to make and submit via a PR.  You must (currently) authorize the build definition first (otherwise the PR will fail).

  1. Push your changes to a branch of the dotnet repository (not your fork)
  2. Edit the build definition
  3. Take note of the "Default branch for manual and scheduled builds"
  4. Change "Default branch for manual and scheduled builds" to the branch you just pushed
  5. Save the build definition.  This will force reauthorization of the "default" branch resoures.
  6. Edit the build definition
  7. Change the "default branch for manual and scheduled builds" back to the value you noted in step 3.
  8. Save the build definition
  9. Now the resources should be authorized and you can submit your changes via a PR or direct push

- "Repository self references endpoint"

  If you see an error like this

  `An error occurred while loading the YAML build definition. Repository self references endpoint 6510879c-eddc-458b-b083-f8150e06ada5 which does not exist or is not authorized for use`

  The problem is the yaml file had a parse error when the definition was originally created. When the definition is created, parse errors are saved with the definition and are supposed to be shown in the definition editor. That regressed in the UI. VSTS is also making a change so that even if there are errors parsing the file, they go ahead and save the repository endpoint as authorized.  In the mean time, you have to track down your YAML parse error.
