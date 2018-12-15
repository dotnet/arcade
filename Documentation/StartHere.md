# Jumping into .NET Core 3 Infrastructure

The purpose of this document is to provide a jumping off point for repository
owners looking to start upgrading their infrastructure to .NET Core 3. It will
provide a general overview of the direction .NET Core 3 infrastructure is going
and a general outline of steps you should take to get started with the upgrade,
with links to appropriate detailed documentation.

## What are we doing?

With every new major product cycle, we take the chance to upgrade our
infrastructure based on what we learned in the previous product cycle. The goal
is to produce a better product more efficiently in the next product cycle.  .NET
Core 3 is no different, though in many ways the infrastructure changes we are
making are much more an overhaul than normal.  Generally, we are focusing in 3
areas:
- **Shared tooling (Arcade)** - Striving to reduce duplication of tooling,
  improve development consistency between repos and drive tooling improvements
  across a wider swath of the ecosystem more quickly.
- **Transitioning to Azure DevOps for public CI, and upgrading official builds**
  - Move away from Jenkins, improve CI reliability, increase the consistency
  between our official and PR builds, and bring first-class workflow for
  internal as well as public changes.
- **Improving our inter-repo dependency version management (Darc)** - Improve
  the rate at which dependencies are updated in repos, improve content
  tracability, etc.

## Does my repository need to be involved?

Generally, if your repo is shipping in .NET Core 3, yes.  For ['Tier
1'](TierOneRepos.md) repos a full transition is required.  While there exist
some special cases (e.g. repos used as submodules in aspnet/universe), we're
striving to move as many people towards the new infrastructure as possible.
- If you use .NET CI (ci.dot.net, ci2.dot.net, ci3.dot.net) you'll need to move
  into Azure DevOps.
- If you pull new dependencies from other repos (e.g. latest
  Microsoft.NETCore.App package), you'll need to onboard onto Arcade and
  dependency flow.
- If you publish dependencies used by other repos, you'll need to onboard onto
  Arcade, so that other repos may consume your outputs.

## Where can I find general information on .NET Core 3 infrastructure?

There is quite a bit of documentation living under the
[Documentation](../Documentation/) folder in the dotnet/arcade repo.  Here are
some highlights

### Concepts and Goals

- [Arcade overview](Overview.md)
- [How dependency flow works in .NET Core
  3](BranchesChannelsAndSubscriptions.md)
- [Roadmap](CorePackages/PackagesRoadmap.md)
- [Versioning rules](CorePackages/Versioning.md)
- [Dependencies Flow Plan](DependenciesFlowPlan.md): Flowing dependencies with Darc, Maestro and BAR.
- [How to Create and Arcade Package](HowToCreatePackages.md)
- [.NET Core Infrastructure Ecosystem Overview](InfrastructureEcosystemOverview.md)
- [Toolset Publish/Consume Contract](PublishConsumeCOntract.md)
- [Servicing](Servicing.md)
- [Toolsets](Toolsets.md)
- [Version Querying and Updating](VersionQueryingAndUpdating.md)

### Tools we are using and how we are using them

#### Code and repository configuration
  - [The Arcade Build SDK](ArcadeSdk.md)
  - GitHub and Azure Repos
    - [Mirroring public projects](AzureDevOps/internal-mirror.md)
    - [Git Sync Tools](GitSyncTools.md)
    - Bots and connectors
  - [Dependency Description Format](DependencyDescriptionFormat.md)
  - [How to See What's the Latest Version of an Arcade Package](SeePackagesLatestVersion.md)

#### Building projects
  - [Telemetry](CorePackages/Telemetry.md)
  - [MSBuild Task Packages](TaskPackages.md)
  - Azure Pipelines: Orchestrating continuous integration
    - [Goals](AzureDevOps/WritingBuildDefinitions.md)
    - [Onboarding to AzDO](AzureDevOps/AzureDevOpsOnboarding.md)
    - [Choosing a Machine Pool](ChoosingAMachinePool.md)
    - Tasks and Templates
  - [Darc](Darc.md): Arcade's dependency management system
  - [Maestro](Maestro.md): CI automation of dependency flow
  - Mission Control

#### Testing projects
  - Helix: [SDK](../src/Microsoft.DotNet.Helix/Sdk/Readme.md), [JobSender](../src/Microsoft.DotNet.Helix/Sdk/Readme.md)
  - Azure Agent pools and queues
  - Docker support

#### Deploying projects
  - [Publishing](CorePackages/Publishing.md)
  - [SignTool](CorePackages/Signing.md) (and Microbuild)
  - BAR


## I'm ready to get started, what do I do?

See the [Arcade Onboarding](Onboarding.md) guide.

## I need help, who should I talk to?

Contact 'dnceng' for additional guidance.  Either @dotnet/dnceng on GitHub, or
dnceng@microsoft.com

## FAQ

- **Where is .NET CI (Jenkins ci.dot.net, ci2.dot.net, ci3.dot.net) going?**

  The current .NET CI instances will remain operational for about another year,
  then will be decomissioned.  This decomission is lined up with the [support
  lifecycle of .NET Core 1.x](https://www.microsoft.com/net/support/policy),
  thus the expected decomission date is June 27, 2019.

- **I used machine X in .NET CI, does such a machine exist in azure devops?**

  In the move from .NET CI, the existing Jenkins static images will not be
  ported.  They are largely opaque and difficult to patch.  Instead, we will
  move towards using [OS
  onboarding](https://dev.azure.com/dnceng/internal/_git/dotnet-helix-machines?path=%2FREADME.md&version=GBmaster)
  to provide a way to declaratively create new machine images with specific
  software and hook them up to dynamically scalable Helix queues. Unfortunately,
  while the OS onboarding is ready to use, the automated link between Helix and
  Azure DevOps is not. In the meantime Azure DevOps provides agents in the
  following ways.
    - Static machines with signing capability
    - Manually scaling Helix queues.
    - Azure DevOps Hosted VM pools.
    - Azure DevOps Hosted Mac pools.

  In addition, we are pushing to bootstrap in native tools (e.g. cmake) rather
  than bake them into the images.  This means that setting up a developer
  machine becomes a simpler process.  Documentation on the process of
  bootstrapping can be found [here](./NativeToolBootstrapping.md).

  See [here](AzureDevOps/AzureDevOpsOnboarding.md#agent-queues) for more information. For
  additional questions contact 'dnceng'. Either @dotnet/dnceng on GitHub, or
  dnceng@microsoft.com.  Also, you can post to the [Arcade Teams channel](https://teams.microsoft.com/l/channel/19%3acf9dc0ac9753432dbac4023239a9965f%40thread.skype/Arcade?groupId=147df318-61de-4f04-8f7b-ecd328c256bb&tenantId=72f988bf-86f1-41af-91ab-2d7cd011db47).

- **My repo is currently on repo-toolset, how can I move into Arcade?**

  https://github.com/dotnet/arcade/blob/master/Documentation/RepoToolset/MigrationToArcade.md

- **Can I run .NET CI and Azure DevOps CI in parallel while I work out the
  kinks?**

  Sure.
