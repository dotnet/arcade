# Onboarding Azure DevOps

- [Onboarding Azure DevOps](#onboarding-azure-devops)
  - [Project Guidance](#project-guidance)
  - [GitHub to DncEng Internal mirror](#github-to-dnceng-internal-mirror)
  - [Azure DevOps Pull Request and CI builds](#azure-devops-pull-request-and-ci-builds)
  - [Azure DevOps service connection](#azure-devops-service-connection)
    - [GitHub connections](#github-connections)
      - [Switching service connections](#switching-service-connections)
    - [Git (internal) connections](#git-internal-connections)
  - [Agent queues](#agent-queues)
    - [External : (Pool Provider: NetCorePublic-Pool)](#external--pool-provider-netcorepublic-pool)
    - [Internal : (Pool Provider: NetCoreInternal-Pool)](#internal--pool-provider-netcoreinternal-pool)
  - [CI badge link](#ci-badge-link)
  - [Signed Builds](#signed-builds)
  - [Generate Graph Files](#generate-graph-files)
  - [Security](#security)
  - [Notes about Yaml](#notes-about-yaml)
  - [Troubleshooting](#troubleshooting)
    - [Known issues](#known-issues)
    - [Queuing builds](#queuing-builds)
      - [YAML](#yaml)
      - [Resource authorization](#resource-authorization)
        - [Unauthorized service endpoints and resources](#unauthorized-service-endpoints-and-resources)
        - [Unauthorized agent pools](#unauthorized-agent-pools)
      - [Self references endpoint](#self-references-endpoint)

## Project Guidance

[Project guidance](./AzureDevOpsGuidance.md) - Covers guidance on naming conventions, folder structure, projects, Pipelines, etc...

## GitHub to DncEng Internal mirror

If your repository has internal builds (that is, the repo Authenticode signs its outputs), you will need to set up a DncEng Internal mirror. This is *required* for internal builds; if your repository only does PR or public CI builds, you can skip this step.

Instructions for setting up the GitHub to dev.azure.com/dnceng/internal mirror are available in the [dev.azure.com/dnceng internal mirror documentation](./internal-mirror.md)

## Azure DevOps Pull Request and CI builds

Azure DevOps has detailed documentation on how to create builds that are linked from GitHub repositories which can be found [here](https://docs.microsoft.com/en-us/azure/devops/build-release/actions/ci-build-github?view=vsts); however, before going through those steps, keep in mind that our process differs from the steps in the official documentation in a few key places:

* The YAML tutorial links to a .NET Core sample repository for an example of a simple `azure-pipelines.yml` file. Instead of using that repository, use [our sample repository](https://github.com/dotnet/arcade-validation).

## Azure DevOps service connection

### GitHub connections

When creating an Azure DevOps build definition with a GitHub source, you will need a GitHub Service Endpoint to communicate with GitHub and setup web hooks.  Teams should use the "dotnet" GitHub app service connection to manage the GitHub <-> Azure DevOps pipeline connection.

The Azure Pipelines app ("dotnet" connection) will work for repos that are a member of the dotnet organization.  If you have a repo that should be supported but does not show up, you will need to work with @dnceng to update repository access for the app.

The "dotnet" service connection is the only dnceng supported connection.  Other available connections may be removed and we strongly encourage devs to create OAuth connections only when needed and to clean them up when they are no longer necessary.

#### Switching service connections

There is no way to update an existing build definition to use a different service connection.  Instead, you will need to deprecate your current build definition (disable triggers, change the name), and create a new build definition with the same properties.  You should then delete your deprecated definition when you are convinced the new definition works as expected.

### Git (internal) connections

See the [dotnet-bot-github-service-endpoint documentation](https://github.com/dotnet/core-eng/blob/master/Documentation/Project-Docs/VSTS/dotnet-bot-github-service-endpoint.md#dotnet-bot-github-service-endpoint)

## Agent queues

We now use Azure Pool Providers to deploy VMs as build agents. Workflows defined in yaml that still use the "phases" syntax will not able to take advantage of this feature.  See [this document](https://github.com/dotnet/arcade/blob/master/Documentation/AzureDevOps/PhaseToJobSchemaChange.md) for details how to migrate if you are in this scenario.

To use an Azure Pool provider, you need to specify both the name of the pool provider and the Helix Queue it uses.  This looks something like:
``` yaml
          name: NetCorePoolNameFromBelow-Pool
          queue: Helix.Queue.From.Below
```          

Current machine pool recommendations:

### External : (Pool Provider: NetCorePublic-Pool)
| OS         | Recommended pool     | Pool Provider Queue(s)     | Notes | 
| ---------- | -------------------- | -------------------------- | ----- |
| Windows_NT | Hosted VS2017 or 2019| BuildPool.Server.Amd64.Open  | Plain Windows: No VS install |
|            |                      | BuildPool.Server.Amd64.ES.VS2017.Open | Spanish OS, Server Sku, VS2017 |
|            |                      | BuildPool.Server.Amd64.VS2017.Open |  |
|            |                      | BuildPool.Server.Amd64.VS2019.Open |  |
|            |                      | BuildPool.Server.Amd64.VS2019.Pre.Open | Latest public preview VS 2019 |
|            |                      | BuildPool.Server.Amd64.VS2019.Pre.ES.Open | Latest public preview VS 2019, Spanish OS |
|            |                      | BuildPool.Server.Amd64.VS2019.BT.Open |  |
|            |                      | BuildPool.Server.Wasm.Open | Web Assembly Build Configuration |
|            |                      | BuildPool.Windows.10.Amd64.Open |  |
|            |                      | BuildPool.Windows.10.Amd64.ES.VS2017.Open |  |
|            |                      | BuildPool.Windows.10.Amd64.VS2017.Open |  |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.Open |  |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.Pre.Open | Latest public preview VS 2019 |
|            |                      | BuildPool.Windows.Amd64.VS2019.Pre.ES.Open | Latest public preview VS 2019, Spanish OS |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.BT.Open |  |
|            |                      | BuildPool.Windows.10.Wasm.Open | Web Assembly Build Configuration |
| Linux      | Hosted Ubuntu 1604   | buildpool.ubuntu.1604.amd64.open | |
| MacOS      | Hosted MacOS         | n/a | |

### Internal : (Pool Provider: NetCoreInternal-Pool)

| OS         | Recommended pool     | Pool Provider Queue(s)      | Notes | 
| ---------- | -------------------- | --------------------------- | ----- |
| Windows_NT | Use Pool Provider -> | BuildPool.Server.Amd64.VS2017 |     |
|            |                      | BuildPool.Server.Amd64.VS2019 |     |
|            |                      | BuildPool.Server.Amd64.VS2017 |     |
|            |                      | BuildPool.Server.Amd64.VS2019 |     |
|            |                      | BuildPool.Server.Amd64.VS2019.BT |  |
|            |                      | BuildPool.Server.Amd64.VS2019.Pre | |
|            |                      | BuildPool.Windows.10.Amd64.VS2017 | |
|            |                      | BuildPool.Windows.10.Amd64.VS2019 | |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.BT| |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.Pre | |
|            |                      | BuildPool.Windows.10.Amd64.VS2019.Xaml| |
| Linux      | Hosted Ubuntu 1604   | buildpool.ubuntu.1604.amd64 |
| MacOS      | Hosted MacOs Internal| n/a | |

A couple of notes:

- [Hosted pool](https://docs.microsoft.com/en-us/azure/devops/pipelines/agents/hosted?view=vsts&tabs=yaml) capabilities

  - Whenever possible, you're always encouraged to use the hosted agent pools for your builds.  These don't come from the same budget as other build agents, and have far more MacOS machines than the Engineering Services team has.  

- BuildPool Helix machines will be defined in the [dnceng internal repo 'dotnet-helix-machines'](https://dnceng.visualstudio.com/internal/_git/dotnet-helix-machines?path=%2Fdefinitions%2Fshared&version=GBmaster).  While this format is a bit dense, it is what is used by the deployment system so represents precisely what was on the machines for a given date of deployment.  As such, the descriptions below are not necessarily up-to-date but do represent what these queues had as of this edit.

- BuildPool.Windows queues:

  - Visual Studio versions are updated to "latest public GA" version on a roughly monthly cadence.  
  - Windows Client RS4 or higher for "*.Windows.10*," or Server 2019 Images used for "*.Server.*" queues
  - 4 cores
  - 512 GB disk space capacity (not SSD)
  - BuildPool.*.Amd64.VS2017 - Visual Studio 2017 15.X
  - Buildpool.*.Amd64.ES.VS2017.Open  - Visual Studio 2017 15.X EN-US on an ES-ES OS
  - BuildPool.*.Amd64.VS2019 - Visual Studio 2019 Enterprise 16.X
  - BuildPool.*.Amd64.VS2019.ES - Visual Studio 2019 Enterprise 16.X on an ES-ES OS.
  - BuildPool.*.Amd64.VS2019.Pre - Visual Studio 2019 Enterprise Preview 16.X
  - BuildPool.*.Amd64.VS2019.BT - Visual Studio 2019 16.X 'BuildTools' SKU (no UI)
  - BuildPool.*.Wasm - Web Assembly Build Configuration

- BuildPool.Ubuntu queues:
  - Ubuntu 16.04
  - Docker 18.09.6
  - 4 cores
  - 512 GB disk space capacity (not SSD)

## CI badge link

The [Azure DevOps CI Build guidance](https://docs.microsoft.com/en-us/azure/devops/pipelines/get-started-yaml?view=vsts#get-the-status-badge) describes how to determine the CI build badge link.

It is recommended that you restrict the CI build status to a particular branch.  This will prevent the badge from reporting Pull Request build status.  Restrict to a branch by adding the `branchName` parameter to your query string.

Example:

```Text
https://dev.azure.com/dnceng/public/_build?definitionId=208&branchName=master
```

## Signed Builds

dev.azure.com/dnceng now has support for signed builds.  Code should be mirrored to dev.azure.com/dnceng/internal as outlined in the [Azure DevOps Guidance](./AzureDevOpsGuidance.md#projects).  See [MovingFromDevDivToDncEng.md](./MovingFromDevDivToDncEng.md) for information about moving signed builds from DevDiv to DncEng.

## Generate Graph Files

Generation of graph files as part of official builds is now supported. See [GeneratingGraphFiles.md](GeneratingGraphFiles.md) for information on how to opt-in to this feature.

## Security

[Security documentation](https://docs.microsoft.com/en-us/azure/devops/build-release/actions/ci-build-github?view=vsts#security-considerations)

It is recommended that you do **NOT** enable the checkbox labeled "Make secrets available to builds of forks".

## Notes about Yaml

- Code reuse

  For *most* teams, it is recommended that you author your yaml to use the [same yaml files for internal, CI, and Pull Request builds](./WritingBuildDefinitions.md).  See https://github.com/dotnet/arcade/blob/master/azure-pipelines.yml, for how this is being done in Arcade with build steps conditioned on "build reason".

- Shared templates

  Arcade currently provides a handful for [shared templates](https://github.com/dotnet/arcade/tree/master/eng/common/templates).

  - [base.yml](https://github.com/dotnet/arcade/blob/master/eng/common/templates/phases/base.yml) defines docker variables, and enables telemetry to be sent for non-CI builds.

- Variable groups

  Build definitions using variable groups in the DevDiv Organization must first be authorized by manually queuing a build and following instructions presented in the portal. Without authorization, the variables will not be set in the build environment. See [Variable groups for Azure Pipelines](https://docs.microsoft.com/en-us/azure/devops/pipelines/library/variable-groups) for more information.

Notes about templates:

- Additional info about templates - https://docs.microsoft.com/en-us/azure/devops/pipelines/process/templates?view=vsts

- Currently, Azure DevOps has a problem with multiple levels of templates where it will change the order of build steps you provide.  That means, for now, use templates judiciously, don't layer templates within templates, and always validate the step order via a CI build.

## Troubleshooting

### Known issues

For a list of known Azure DevOps issues we are tracking, please go [here](https://dev.azure.com/dnceng/internal/_queries/query/7275f17c-c42f-44b8-9798-9c2426bf8395/)

### Queuing builds

#### YAML

- YAML: "The array must contain at least one element. Parameter name: jobs"

  If your template doesn't compile, then it may prevent any of your "job" elements from surfacing which leads to this error.  This error hides what the real error in the template is.  You can work around this error by providing a default job.

  ```YAML
  jobs:
  - job: foo
    steps:
    - script: echo foo
  ```

  With a default job provided, when you queue a build, Azure DevOps will now tell you the actual error you care about.  Azure DevOps is hotfixing this issue so that the lower-level issue is surfaced.

#### Resource authorization

  If you made some change to a resource or changed resources, but everything else *appears* correct (from a permissions / authorization point of view), and you're seeing "resource not authorized" when you try to queue a build; it's possible that the resource is fine, but the build pipeline is not authorized to use it.  Edit the build pipeline and make some minor change, then save.  This will force the pipeline to re-authorize and you can undo whatever minor change you made.

  Note that [resource authorization](https://docs.microsoft.com/en-us/azure/devops/pipelines/process/resources?view=vsts#resource-authorization) happens on Push, not for Pull Requests.  If you have some changes to resources that you want to make and submit via a PR.  You must (currently) authorize the Pipeline first (otherwise the PR will fail).

##### Unauthorized service endpoints and resources

- "Resource not authorized" or "The service endpoint does not exist or has not been authorized for use"

  1. Push your changes to a branch of the dnceng repository (not your fork)
  2. Edit the Pipeline
  3. Take note of the "Default branch for manual and scheduled builds"
  4. Change "Default branch for manual and scheduled builds" to the branch you just pushed
  5. Save the Pipeline.  This will force reauthorization of the "default" branch resources.
  6. Edit the Pipeline
  7. Change the "default branch for manual and scheduled builds" back to the value you noted in step 3.
  8. Save the Pipeline
  9. Now the resources should be authorized and you can submit your changes via a PR or direct push

##### Unauthorized agent pools

  1. Push your changes to a branch of the dnceng repository (not your fork)
  2. Edit the Pipeline
  3. Take note of the "Default branch for manual and scheduled builds"
  4. Change "Default branch for manual and scheduled builds" to the branch you just pushed
  5. Take note of the "Default agent pool"
  6. Change the "Default agent pool" to the pool that is unauthorized.
  7. Save the Pipeline.  This will force reauthorization of the "default" branch resources.
  8. Edit the Pipeline
  9. Change the "default branch for manual and scheduled builds" back to the value you noted in step 3 and the default agent pool back to the value you noted in step 4 (or search for previous values in the Pipeline "History" tab.
  10. Save the Pipeline
  11. Now the resources should be authorized and you can submit your changes via a PR or direct push

#### Self references endpoint

- "Repository self references endpoint"

  If you see an error like this

  `An error occurred while loading the YAML Pipeline. Repository self references endpoint 6510879c-eddc-458b-b083-f8150e06ada5 which does not exist or is not authorized for use`

  The problem is the yaml file had a parse error when the definition was originally created. When the definition is created, parse errors are saved with the definition and are supposed to be shown in the definition editor. That regressed in the UI. Azure DevOps is also making a change so that even if there are errors parsing the file, they go ahead and save the repository endpoint as authorized.  In the mean time, you have to track down your YAML parse error.
