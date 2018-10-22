# Onboarding VSTS

- [Project Guidance](#project-guidance)
- [GitHub to DncEng Internal mirror](#github-to-dnceng-internal-mirror)
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

## GitHub to DncEng Internal mirror

If your repository has internal builds, you will need to set up a DncEng Internal mirror. This is *required* for internal builds; if your repository only does PR or public CI builds, you can skip this step.

Instructions for setting up the GitHub to dev.azure.com/dnceng/internal mirror are available in the [dev.azure.com/dnceng internal mirror documentation](./internal-mirror.md)

## VSTS Pull Request and CI builds

VSTS has detailed documentation on how to create builds that are linked from GitHub repositories which can be found [here](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts); however, before going through those steps, keep in mind that our process differs from the steps in the official documentation in a few key places:

* The YAML tutorial links to a .NET Core sample repository for an example of a simple `.vsts-ci.yml` file. Instead of using that repository, use [our sample repository](https://github.com/dotnet/arcade-minimalci-sample).
* VSTS will require a GitHub Service Endpoint to communicate with github and setup web hooks.  Teams should use the `DotNet-Bot GitHub Connection` Service Endpoint.  The `DotNet-Bot GitHub Connection` requires that teams add the .NET Core owned [service account](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/dotnet-bot-github-service-endpoint.md#github-service-account) as a [collaborator](https://help.github.com/articles/permission-levels-for-a-user-account-repository/#collaborator-access-on-a-repository-owned-by-a-user-account) (Admin access) on the GitHub repo.

For implementation details and managing information about `DotNet-Bot GitHub Connection` see the [documentation](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/dotnet-bot-github-service-endpoint.md#vsts-service-endpoint)

## Agent queues

Agent queue use / configuration / etc... is likely to change very soon when Azure DevOps enables "bring your own cloud".

Current machine pool recommendations:

### External

| OS         | Recommended pool     | Additional pool option     |
| ---------- | -------------------- | -------------------------- |
| Windows_NT | dotnet-external-temp |                            |
| Linux      | Hosted Ubuntu 1604   | dnceng-linux-external-temp |
| OSX        | Hosted Mac Preview   | |

### Internal

| OS         | Access   | Recommended pool     | Additional pool option |
| ---------- | -------- | ---------------------| ---------------------- |
| Windows_NT | Internal | dotnet-internal-temp | |
| Linux      | Internal | Hosted Ubuntu 1604   | dnceng-linux-internal-temp |
| OSX        | Internal | Hosted Mac Internal  | |

A couple of notes:

- [Hosted pool](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=vsts&tabs=yaml) capabilities

- dotnet-external-temp and dotnet-internal-temp queues:

  - Windows Server 2016

  - 4 cores

  - 512 GB disk space capacity (not SSD)

  - Visual Studio 2017 15.8

- dnceng-linux-external-temp and dnceng-linux-internal-temp queues:

  - Ubuntu 16.04

  - Docker 17.12.1

  - 512 GB disk space capacity (not SSD)

## CI badge link

The [VSTS CI Build guidance](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts#create-a-vsts-build-status-with-a-github-readme-file) describes how to determine the CI build badge link, but only for task based build definitions.  If you're using a YAML based build definition, then you can determine the badge link by either of these two methods.

- Edit the build definition and go to the "History" tab.  Select the most recent change, right-click, and select "Compare Differences".  Scroll through the json and look for the "\_links" section to find the "badge" link.

```JSON
"_links": {
  "self": {
      "href": "https://dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_apis/build/Definitions/15?revision=4"
  },
  "web": {
      "href": "https://dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_build/definition?definitionId=15"
  },
  "editor": {
      "href": "https://dev.azure.com/dnceng/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/_build/designer?id=15&_a=edit-build-definition"
  },
  "badge": {
      "href": "https://dev.azure.com/dnceng/_apis/public/build/definitions/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/15/badge"
  }
},
```

- Use the method provided in the [CI Build Guidance](https://docs.microsoft.com/en-us/vsts/build-release/actions/ci-build-github?view=vsts#create-a-vsts-build-status-with-a-github-readme-file) to discover the badge link for a task based build definition in the same project, then replace the build definition id with the build definition id of the yaml based build you are modifying.  The Url encoding follows this pattern...

  `https://[project collection]/_apis/[project name]/build/definitions/[project id]/[build definition id]/badge`

It is recommended that you restrict the CI build status to a particular branch.  This will prevent the badge from reporting Pull Request build status.  Restrict to a branch by adding the `branchName` parameter to your query string.

Example:

```Text
https://dev.azure.com/dnceng/public/_build/index?definitionId=17&branchName=master
```

## Signed Builds

dev.azure.com/dnceng now has support for signed builds.  Code should be mirrored to dev.azure.com/dnceng/internal as outlined in the [VSTS Guidance](./VSTSGuidance.md#projects).  See [MovingFromDevDivToDncEng.md](./MovingFromDevDivToDncEng.md) for information about moving signed builds from DevDiv to DncEng.

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

For a list of known VSTS issues we are tracking, please go [here](https://dev.azure.com/dnceng/internal/_queries/query/7275f17c-c42f-44b8-9798-9c2426bf8395/)

### Queuing builds

#### YAML

- YAML: "The array must contain at least one element. Parameter name: phases"

  If your template doesn't compile, then it may prevent any of your "phase" elements from surfacing which leads to this error.  This error hides what the real error in the template is.  You can work around this error by providing a default phase.

  ```YAML
  phases:
  - phase: foo
    steps:
    - script: echo foo
  ```

  With a default phase provided, when you queue a build, VSTS will now tell you the actual error you care about.  VSTS is hotfixing this issue so that the lower-level issue is surfaced.

#### Resource authorization

  If you made some change to a resource or changed resources, but everything else *appears* correct (from a permissions / authorization point of view), and you're seeing "resource not authorized" when you try to queue a build; it's possible that the resource is fine, but the build definition is not authorized to use it.  Edit the build definition and make some minor change, then save.  This will force the build definition to re-authorize and you can undo whatever minor change you made.

  Note that [resource authorization](https://github.com/Microsoft/vsts-agent/blob/d792192875381ea770f09f3740ed8d1051f4f456/docs/preview/yamlgettingstarted-authz.md) happens on Push, not for Pull Requests.  If you have some changes to resources that you want to make and submit via a PR.  You must (currently) authorize the build definition first (otherwise the PR will fail).

##### Unauthorized service endpoints and resources

- "Resource not authorized" or "The service endpoint does not exist or has not been authorized for use"

  1. Push your changes to a branch of the dnceng repository (not your fork)
  2. Edit the build definition
  3. Take note of the "Default branch for manual and scheduled builds"
  4. Change "Default branch for manual and scheduled builds" to the branch you just pushed
  5. Save the build definition.  This will force reauthorization of the "default" branch resoures.
  6. Edit the build definition
  7. Change the "default branch for manual and scheduled builds" back to the value you noted in step 3.
  8. Save the build definition
  9. Now the resources should be authorized and you can submit your changes via a PR or direct push

##### Unauthorized agent pools

  1. Push your changes to a branch of the dnceng repository (not your fork)
  2. Edit the build definition
  3. Take note of the "Default branch for manual and scheduled builds"
  4. Change "Default branch for manual and scheduled builds" to the branch you just pushed
  5. Take note of the "Default agent pool"
  6. Change the "Default agent pool" to the pool that is unauthorized.
  7. Save the build definition.  This will force reauthorization of the "default" branch resoures.
  8. Edit the build definition
  9. Change the "default branch for manual and scheduled builds" back to the value you noted in step 3 and the default agent pool back to the value you noted in step 4 (or search for previous values in the build definition "History" tab.
  10. Save the build definition
  11. Now the resources should be authorized and you can submit your changes via a PR or direct push

#### Self references endpoint

- "Repository self references endpoint"

  If you see an error like this

  `An error occurred while loading the YAML build definition. Repository self references endpoint 6510879c-eddc-458b-b083-f8150e06ada5 which does not exist or is not authorized for use`

  The problem is the yaml file had a parse error when the definition was originally created. When the definition is created, parse errors are saved with the definition and are supposed to be shown in the definition editor. That regressed in the UI. VSTS is also making a change so that even if there are errors parsing the file, they go ahead and save the repository endpoint as authorized.  In the mean time, you have to track down your YAML parse error.
