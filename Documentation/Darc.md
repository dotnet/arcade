# Darc 

Darc is a tool for managing and querying the relationships between repositories
in the .NET Core ecosystem. This document describes various scenarios and how to
use darc to achieve them, as well as a general reference guide to darc commands.

## Description

*  Darc is meant to be the **only** way developers and other tools like Maestro++ interact and alter version/dependency files 
as well as bootstrapping files and scripts in arcade participating repos.
*  Darc's operations range from altering version/dependency files to creating PRs in specified repos.

## Scenarios

### Setting up your darc client

### Adding dependencies to a repository

### Removing dependencies from a repository

### 'Pinning' dependencies so they do not update.

### Updating dependencies in your local repository

### Adding dependency flow

### Branching for releases

### Halting and restarting dependency flow

### Gathering a build drop

### Viewing the dependency graph

## Command Reference

### **`Common parameters`**

There are a few common parameters available on every command:

- `-p, --password` - Build Asset Registry password.  You can obtain this
  password by going to https://maestro-prod.westus2.cloudapp.azure.com/, logging
  in using the link in the top right, then generating a token using the menu in
  the top right.  This setting overrides whatever BAR password was provided when
  doing `darc authenticate`.
- `--github-pat` - Person access token used to authenticate GitHub. This is a GitHub PAT used
  to avoid rate limiting when accessing github to download arcade script files
  or version files. You only need a GitHub PAT with **no** authorization scopes
  checked. This setting overrides whatever BAR password was provided when
  doing `darc authenticate`.
- `--azdev-pat` - Personal access token used to authenticate to Azure DevOps.
  This token should have Code Read permissions. This setting overrides whatever BAR password was provided when
  doing `darc authenticate`.
- `--bar-uri` - URI of the build asset registry service to use.  Typically left
  as its default (https://maestro-prod.westus2.cloudapp.azure.com) This setting overrides whatever BAR password was provided when
  doing `darc authenticate`.
- `--verbose` - Turn on additional output.
- `--debug` - Turn on debug output
- `--help` - Display help
- `--version` - Display version of darc.

Individual darc commands are described below.

### **`add-channel`**

Add a new channel. This creates a new tag that builds can be applied to.

*This is not a typical operation and you should consult with the (`@dnceng`)
engineering team before doing so.*

**Sample**:
```
PS D:\enlistments\arcade> darc add-channel --name "Foo"

Successfully created new channel with name 'Foo'.
```

**Parameters**

- `-n, --name` -  **(Required)**. Name of channel to create.
- `-c, --classification` - Classification of channel. Defaults to 'dev'.  Today,
  this classification does not affect any functionality
- `-i, --internal` - Channel is internal only. This option is currently
  non-functional

**See also**:
- [delete-channel](#delete-channel)
- [get-channels](#get-channels)

### **`add`**

Add a new tracked dependency to the Version.Detail.xml file in your local repo.
This new dependency can then be updated using
[update-dependencies](#update-dependencies). After merging the changes into
the remote github or AzDO repository, the dependency can be updated by Maestro++
if there is a corresponding subscription targeting that repo.

When adding a new dependency, only name and type are required.  For a detailed
discussion on adding new dependencies to a repository, see [Adding dependencies to a repository](#adding-dependencies-to-a-repository)

**Parameters**

- -n, --name - **(Required)** Name of dependency to add. This is the name of the
  package you wish to track.  For example, this might be "Microsoft.NETCore.App"
  or 'System.Security.Cryptography.Cng'
- -t, --type - **(Required)** 'toolset' or 'product'. See [Adding dependencies
  to a repository](#adding-dependencies-to-a-repository) for a discussion on
  dependency types.
- -v, --version - Dependency version.
- -r, --repo - Repository where the dependency was built.
- -c, --commit - SHA at which the dependency was produced.

**Sample**

*eng\Version.Details.xml* before running add:

```
PS D:\enlistments\arcade> cat .\eng\Version.Details.xml
<?xml version="1.0" encoding="utf-8"?>
<Dependencies>
  <ProductDependencies></ProductDependencies>
  <ToolsetDependencies>
    <Dependency Name="Microsoft.DotNet.Arcade.Sdk" Version="1.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Build.Tasks.Feed" Version="2.2.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Maestro.Tasks" Version="1.0.0-beta.19060.8">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>67384d20d310611afc1c2b4dd3b953fda182def4</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.SignTool" Version="1.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Helix.Sdk" Version="2.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
  </ToolsetDependencies>
</Dependencies>
```

Running add

```
PS D:\enlistments\arcade> darc add --name "Microsoft.NETCore.App" --type "product" --version 1 --commit 2 --repo https://github.com/dotnet/core-setup
```

*eng\Version.Details.xml* after add:

```
PS D:\enlistments\arcade> cat .\eng\Version.Details.xml
<?xml version="1.0" encoding="utf-8"?>
<Dependencies>
  <ProductDependencies>
    <Dependency Name="Microsoft.NETCore.App" Version="1">
      <Uri>https://github.com/dotnet/core-setup</Uri>
      <Sha>2</Sha>
    </Dependency>
  </ProductDependencies>
  <ToolsetDependencies>
    <Dependency Name="Microsoft.DotNet.Arcade.Sdk" Version="1.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Build.Tasks.Feed" Version="2.2.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Maestro.Tasks" Version="1.0.0-beta.19060.8">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>67384d20d310611afc1c2b4dd3b953fda182def4</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.SignTool" Version="1.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
    <Dependency Name="Microsoft.DotNet.Helix.Sdk" Version="2.0.0-beta.19080.6">
      <Uri>https://github.com/dotnet/arcade</Uri>
      <Sha>14d1133b6074b463784a7adbbf385df0462f4010</Sha>
    </Dependency>
  </ToolsetDependencies>
</Dependencies>
```

**See also**:
- [update-dependencies](#update-dependencies)
- [Adding dependencies to a repository](#adding-dependencies-to-a-repository)

### **`add-default-channel`**

Adds a new default channel mapping.  A default channel maps each new build of a specific
branch of a repository onto a specific channel. While builds can be selectively
and manually applied to channels, this is generally inconvenient for day to day development
in most cases.  In general, until release shutdown, each build of a branch
should always be applied to its "normal" channel.,

***Note that the branch specified should almost always be
"refs/heads/{branchName}", unless you explicitly know otherwise***.

This is because the BAR reporting functionality pulls the branch name from the
Azure Devops built in pipeline variables, which specify refs/heads/foo vs. foo.
If your repository is manually reporting to BAR without using the Arcade
templates, then this name may be different.

Default channel mappings can be deleted with [delete-default-channel](#delete-default-channel).

**Parameters**
- `--channel` - **(Required)** Name of channel that a build of 'branch' and 'repo' should be applied to.
- `--branch` - **(Required)** Build of 'repo' on this branch will be
  automatically applied to 'channel'.  Should generally be "refs/heads/branchName"
- `--repo` - **(Required)** Build of this repo repo on 'branch' will be automatically applied to 'channel'

**Sample**
```
PS D:\enlistments\arcade> darc add-default-channel --channel ".Net Core 3 Dev" --branch refs/heads/master --repo https://github.com/dotnet/arcade
```

**See also**:
- [get-channels](#get-channels)
- [get-default-channels](#get-default-channels)
- [delete-default-channel](#delete-default-channel)

### **`add-subscription`**

Adds a new subscription to Maestro++.

A subscription describes an update
operation for a specific repository+branch combination, mapping outputs of a
repository that have beeen applied to a channel (virtual branch) onto matching
inputs of the target repository+branch.

For example, a build of dotnet/corefx might be applied to the ".NET Core 3 Dev"
channel. dotnet/core-setup maps new outputs of corefx on the ".NET Core 3 Dev"
channel onto its master branch.

A subscription has a few parts:
- Mapping of source repo + source channel => target repo target branch
- An update rate (e.g. every day, every build, not at all)
- Whether a subscription is batchable or not. If batchable, all batchable
  subscriptions targeting the same branch/repo combination will share a PR.
  *Note: Batchable subscriptions are currently unsupported in darc*
- A set of auto merge policies, if the subscription is not batchable.  If batchable,
  merge policies are set on a repository level rather than a per-subscription
  level, as they end up shared between several subscriptions. *Note: repository
  merge policies are currently unsupported in darc*
  
`add-subscription` has two modes of operation
- Interactive mode (default) - Interactive mode will take whatever input parameters were
  provided on the command line (if any) and pop an editor where the user can
  provide the subscription input prameters.
- Command-line only mode (`-q`) - In this mode, the full set of input options must be
 supplied.

Upon saving and closing the editor, or running the darc command if in command
line mode (`-q`), the darc tool submits the new subscription to Maestro++. If
successful, the id of the new subscription is returned.

**Parameters**

- `-channel` - **(Required if -q is passed)** Name of channel that is the source of the subscription. For a
  list of channels, see [get-channels](#get-channels)
- `--source-repo` - **(Required if -q is passed)** Source repository for the subscription.  Builds of this
  repository that appear on the specified `--channel` will have matching outputs
  applied to the inputs (specified in eng/Version.Details.xml) of `--target-repo` and `--target-branch`.
- `--target-repo` - **(Required if -q is passed)** Target repository for the subscription.  Builds of
  `--source-repo` that appear on the specified `--channel` will have matching
  outputs applied to the inputs (specified in eng/Version.Details.xml) of this
  repo's `--target-branch`
- `--target-branch` - **(Required if -q is passed)** Target branch for the subscription. Builds of
  `--source-repo` that appear on the specified `--channel` will have matching
  outputs applied to the inputs (specified in eng/Version.Details.xml) on this
  branch of `--target-repo`.
- `--update-frequency` - **(Required if -q is passed)** Frequency of updates. Valid values are: 'none',
  'everyDay', or 'everyBuild'.  Every day is applied at 5am.  Subscriptions with
  'none' frequency can still be triggered using [trigger-subscriptions](#trigger-subscriptions)
- `--all-checks-passed` - Merge policy. A PR is automatically merged by Maestro++ if there is at least one
  check and all are passed. Optionally provide a comma separated list of
  ignored check with --ignore-checks.
- `--ignore-checks` - Merge policy. A For use with --all-checks-passed. A set of checks that are
  ignored. Typically, in github repos the "WIP" and "license/cla" checks are ignored.
- `--no-extra-commits`- Merge policy. A PR is automatically merged if no non-bot commits exist in the PR.
- `--require-checks` - Merge policy. A PR is automatically merged if the specified checks are passed. Provide a comma separate list of required checks.
- `-q, --quiet` - Non-interactive mode (requires all elements to be passed on the command line).

**Sample**:
```
PS D:\enlistments\arcade-services> darc add-subscription --channel ".NET Tools - Latest" --source-repo https://github.com/dotnet/arcade --target-repo https://dev.azure.com/dnceng/internal/_git/dotnet-optimization --target-branch master --update-frequency everyDay --all-checks-passed -q

Successfully created new subscription with id '4f300f68-8800-4b14-328e-08d68308fe30'.
```

**Available merge policies**

- AllChecksSuccessful - All PR checks must be successful, potentially ignoring a
  specified set of checks. Checks might be ignored if they are unrelated to PR
  validation. The check name corresponds to the string that shows up in GitHub/Azure DevOps.
  
  YAML format for interactive mode:
  ```
   - Name: AllChecksSuccessful
     Properties:
       ignoreChecks:
       - WIP
       - license/cla
       - <other check names>
  ```
   
- RequireChecks - Require that a specific set of checks pass. The check name
  corresponds to the string that shows up in GitHub/Azure DevOps.
  
  YAML format for interactive mode:
  ```
   - Name: RequireChecks
     Properties:
       checks:
       - MyCIValidation
       - CI
       - <other check names>
  ```
   
- NoExtraCommits - If additional non-bot commits appear in the PR, the PR should not be merged.

  YAML format for interactive mode:
  ```
   - Name: NoExtraCommits
  ```

**See also**:
- [delete-subscription](#delete-subscription)
- [get-subscriptions](#get-subscriptions)
- [trigger-subscriptions](#trigger-subscriptions)
- [get-channels](#get-channels)

### **`authenticate`**

Set up your darc client so that the PAT or password inputs do not need to be
passed on each command invocation.  This command opens up an editor form with
various password settings. These values are overridden by the `--password`,
`--bar-uri`, `--azdev-pat` and `--github-pat` settings common to all commands.

See [Setting up Your Darc Client](#setting-up-your-darc-client) for more
information.

**Parameters**

None.

**Sample**
```
PS D:\enlistments\arcade> darc authenticate

(opens in editor)

# Create new BAR tokens at https://maestro-prod.westus2.cloudapp.azure.com/Account/Tokens
bar_password=***
# Create new GitHub personal access tokens at https://github.com/settings/tokens (no auth scopes needed)
github_token=***
# Create new Azure Dev Ops tokens at https://dev.azure.com/dnceng/_details/security/tokens (code read scope needed)
azure_devops_token=***
build_asset_registry_base_uri=https://maestro-prod.westus2.cloudapp.azure.com/

# Storing the required settings...
# Set elements above depending on what you need

```

### **`delete-channel`**

Delete a channel. This channel must be in use by any subscriptions.

*This is not a typical operation and you should consult with the (`@dnceng`)
engineering team before doing so.*

**Parameters**

- `-n, --name` - **(Required)** Name of channel to delete.

**Sample**:
```
PS D:\enlistments\arcade> darc delete-channel --name "Foo"

Successfully deleted channel 'Foo'.
```

**See also**:
- [add-channel](#add-channel)
- [get-channels](#get-channels)

### **`delete-default-channel`**

Deletes a default channel mapping. Deleting will not affect any existing builds,
but new builds of the specified repos will will not be applied to the target
channel.

You can obtain a list of current default channel mappings with
[get-default-channels](#get-default-channels)

- `--channel` - **(Required)** Name of channel that builds of 'repository' and 'branch' should not apply to.
- `--branch` - **(Required)** Repository that should have its default association removed.
- `--repo` - **(Required)** Branch that should have its default association
  removed.

**Sample**
```
PS D:\enlistments\arcade> darc delete-default-channel --channel ".Net Core 3 Dev" --branch refs/heads/master --repo https://github.com/dotnet/arcade
```

**See also**:
- [add-default-channel](#add-default-channel)
- [get-default-channels](#get-default-channels)

### **`delete-subscription`**

Deletes a specified subscription by its id. This removes the subscription from
Maestro and no new updates based on the subscription will be created. Any
updates currently in progress will not be closed, but will not auto-merge.  To
obtain the id of a subscription to be deleted, see [get-subscriptions](#get-subscriptions).

**Sample**:
```
PS D:\enlistments\arcade-services> darc delete-subscription --id 4f300f68-8800-4b14-328e-08d68308fe30

Successfully deleted subscription with id '4f300f68-8800-4b14-328e-08d68308fe30'
```

**See also**:
- [add-subscription](#add-subscription)
- [get-subscriptions](#get-subscriptions)
- [trigger-subscriptions](#trigger-subscriptions)

### **`gather-drop`**

### **`get-channels`**

Retrieves a list of channels. Channels are something like a virtual cross
repository branch. They are a tag that is applied to a build which indicates the
purpose of the outputs of that build. Channels are used as sources in a
subscription, indicating that the repository wants dependency updates from
builds meant for the purpose associated with the channel.

For instance, there is a channel called `.NET Core 3 Dev`. Builds that appear on
this channel are intended for day to day .NET Core 3 development. Repositories
may have dependencies on other .NET Core repositories when building their own
part of the .NET Core 3 stack. By subscribing to that repository's `.NET Core 3
Dev` channel, they map .NET Core 3 daily development outputs onto their own
target branch.

**Parameters**

None.

**Sample**:
```
PS D:\enlistments\arcade> darc get-channels

.NET Tools - Latest
.NET Core 3 Dev
.NET Engineering Services - Int
.NET Engineering Services - Prod
.NET Tools - Validation
```

### **`get-default-channels`**

Retrieves a list of default channel mappings. A default channel maps each new build of a specific
branch of a repository onto a specific channel. While builds can be selectively
and manually applied to channels, this is generally inconvenient for day to day development
in most cases.  In general, until release shutdown, each build of a branch
should always be applied to its "normal" channel.

**Parameters**

None.

**Sample**
```
PS D:\enlistments\arcade> darc get-default-channels

https://devdiv.visualstudio.com/DevDiv/_git/DotNet-Trusted @ refs/heads/master -> .NET Core 3 Dev
https://github.com/aspnet/AspNetCore @ master -> .NET Core 3 Dev
https://github.com/aspnet/AspNetCore @ refs/heads/master -> .NET Core 3 Dev
https://github.com/aspnet/AspNetCore-Tooling @ refs/heads/master -> .NET Core 3 Dev
https://github.com/aspnet/EntityFrameworkCore @ refs/heads/master -> .NET Core 3 Dev
https://github.com/aspnet/Extensions @ refs/heads/master -> .NET Core 3 Dev
https://github.com/aspnet/websdk @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/arcade @ refs/heads/master -> .NET Tools - Validation
https://github.com/dotnet/cli @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/cli-migrate @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/CliCommandLineParser @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/core-sdk @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/core-setup @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/coreclr @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/corefx @ master -> .NET Core 3 Dev
https://github.com/dotnet/roslyn @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/sdk @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/standard @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/symreader @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/symreader-portable @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/templating @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/test-templates @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/toolset @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/winforms @ refs/heads/master -> .NET Core 3 Dev
https://github.com/dotnet/wpf @ refs/heads/master -> .NET Core 3 Dev
https://github.com/Microsoft/msbuild @ refs/heads/master -> .NET Core 3 Dev
https://github.com/Microsoft/visualfsharp @ refs/heads/dev16.1 -> .NET Core 3 Dev
https://github.com/Microsoft/vstest @ refs/heads/master -> .NET Core 3 Dev
```

### **`get-dependencies`**

### **`get-dependency-graph`**

### **`get-subscriptions`**

Retrives information about existing subscriptions. This command is generally
useful to determine what kind of dependency flow will happen on new builds, or
to obtain the id of a subscription for use in
[delete-subscription](#delete-subscription).

The top line of the listing shows the subscription mapping and is read:
```
https://github.com/aspnet/AspNetCore (.NET Core 3 Dev) ==> 'https://github.com/dotnet/core-sdk' ('master')

Builds of https://github.com/aspnet/AspNetCore that have been applied to channel ".NET Core 3 Dev" will be applied to the master branch of https://github.com/dotnet/core-sdk.
```

**Parameters**

If no parameters are specified, `get-subscriptions` will show a full list of
Maestro++ subscriptions. This list can be filtered by various input parameters
to be more useful.

- `--target-repo` - Filter by target repo (matches substring unless --exact or --regex is passed).
- `--source-repo` - Filter by source repo (matches substring unless --exact or --regex is passed).
- `--channel` - Filter by source channel (matches substring unless --exact or --regex is passed).
- `--target-branch` - Filter by target branch (matches substring unless --exact or --regex is passed).
- `--exact` - Match subscription parameters exactly (cannot be used with --regex).
- `--regex` - Match subscription parameters using regex (cannot be used with --exact).

**Sample**:
```
PS D:\enlistments\arcade-services> darc get-subscriptions --target-repo core-sdk --source-repo aspnet

https://github.com/aspnet/AspNetCore (.NET Core 3 Dev) ==> 'https://github.com/dotnet/core-sdk' ('master')
  - Id: 70b86840-e31e-4be9-d5d5-08d670f9e862
  - Update Frequency: everyDay
  - Merge Policies:
    AllChecksSuccessful
      ignoreChecks =
                     [
                       "WIP",
                       "license/cla"
                     ]
  - Last Build: N/A
https://github.com/aspnet/EntityFrameworkCore (.NET Core 3 Dev) ==> 'https://github.com/dotnet/core-sdk' ('master')
  - Id: 07401c84-7cc6-41dd-8c40-08d66611bea4
  - Update Frequency: everyDay
  - Merge Policies:
    AllChecksSuccessful
      ignoreChecks =
                     [
                       "WIP",
                       "license/cla"
                     ]
  - Last Build: N/A
```

**See also**:
- [add-subscription](#add-subscription)
- [get-subscriptions](#get-subscriptions)
- [trigger-subscriptions](#trigger-subscriptions)

### **`trigger-subscriptions`**

Triggers one or more subscriptions. For each subscription triggered, Maestro++
will determine whether the latest build on the source channel of the source repository has been applied (or is currently in PR)
to the target repo and branch. If not, a new PR will be created or updated
(depending on existing PRs and/or subscription batchability).

This update is not asynchronous and usually takes a few minutes, as Maestro++ needs
to do a fair bit of work in the background.  New PRs created by
trigger-subscriptions have `dotnet-maestro[bot]` as their author.

Like get-subscriptions, `trigger-subscriptions` takes a number of input parameters
to filter the available subscriptions to the desired set, though at least one
input must be specified. Unless `-q, --quiet` is specified, darc will ask for
confirmation before sending the trigger request.

**Parameters**

- `--id` - Trigger subscription by id.  Not compatible with other filtering parameters.
- `--target-repo` - Filter by target repo (matches substring unless --exact or --regex is passed).
- `--source-repo` - Filter by source repo (matches substring unless --exact or --regex is passed).
- `--channel` - Filter by source channel (matches substring unless --exact or --regex is passed).
- `--target-branch` - Filter by target branch (matches substring unless --exact or --regex is passed).
- `--exact` - Match subscription parameters exactly (cannot be used with --regex).
- `--regex` - Match subscription parameters using regex (cannot be used with
  --exact).
- `-q, --quiet` - Trigger subscriptions without confirmation.  Be careful!

**Sample**:
```
PS D:\enlistments\arcade> darc trigger-subscriptions --source-repo arcade --target-repo arcade-services

Will trigger the following 1 subscriptions...
  https://github.com/dotnet/arcade (.NET Tools - Latest) ==> 'https://github.com/dotnet/arcade-services' ('master')
Continue? (y/n) y
Triggering 1 subscriptions...done
```

**See also**:
- [add-subscription](#add-subscription)
- [get-subscriptions](#get-subscriptions)
- [trigger-subscriptions](#trigger-subscriptions)

### **`update-dependencies`**

### **`verify`**