# Darc

## Description

*  Darc is meant to be the **only** way developers and other tools like Maestro++ interact and alter version/dependency files 
as well as bootstrapping files and scripts in arcade participating repos.
*  Darc's operations range from altering version/dependency files to creating PRs in specified repos.

## Scenarios 

### Arcade updating its Arcade dependency using Maestro++ and Darc

1.  An Official Build for Arcade happens that i.e. creates XX package with vXY
    1. At the end of the build, the Reporting Store gets updated with the information of the new package produced and its dependencies
    by a "publishing" task within the build definition.
2.  Maestro++ trigger happens
3.  Maestro++ uses Darc to ask who has a dependency on `arcade.sdk`
    1. Maestro++ calls `get -d --remote -n arcade.sdk`
4.  For each repo/branch that depends on `arcade.sdk`, Maestro++ uses Darc to check the current version of that package in that repository
    1. Maestro++ calls `get -l --remote -r repo-uri -b branch`
5.  Maestro++ determines if there is a need to update the dependency
    1. Maestro++ calls Darc asking to update the version of `arcade.sdk` to vXY
        1. Darc creates a PR into the specified repository and assigns as owner Maestro++ user/bot
        2. Dev/Maestro++ merges the PR

### Dev updates a set of files that need to be pushed to master branch in repos A, B and C

1.  Dev makes changes in files eng\common\F1, eng\F2 and eng\common\folder\F3
2.  Dev creates a RepoFile.xml where repos A, B and C are defined and include F1, F2 and F3 in the FileMapping node
3.  Dev executes the command `darc push -r "E:\RepoFile.xml" -t 123f1234ed123ccc123f236e12b1234a456b987`
    1. Darc creates a PR in the master branch of repos A, B and C
    2. Dev merges the PR

### Dev updates a set of files that need to be pushed to default repos and branches

1.  Dev makes changes in files eng\common\F1, eng\F2 and eng\common\folder\F3
2.  Dev executes the command `darc push -t 123f1234ed123ccc123f236e12b1234a456b987`
    1. Darc pulls the default repos.xml file (still TBD)
        1. Darc creates a PR in the branches and repos defined in default repos.xml
        2. Dev merges the PR

### Dev add a new dependency to master branch of coreclr

1.  Dev executes `darc add -n Microsoft.DotNet.Build.Tasks.Feed -v 1.0.1`
2.  Since the developer only wants this change to be pushed to the same branch and repo the developer creates a repo.xml containing just 
this branch+repo
3.  Dev executes the command `darc push -r "E:\RepoFile.xml" -t 123f1234ed123ccc123f236e12b1234a456b987`
    1. Darc creates a PR in the master branch of coreclr
    2. Dev merges the PR

## version/dependency files

The version/dependency files define a set of versioned items which the repo depends on. These files are:

#### eng\Version.Details.xml
```xml
<?xml version="1.0" encoding="utf-8"?>
<Dependencies>
    <!-- Elements contains all product dependencies -->
    <ProductDependencies>
        <-- All product dependencies are contained in Version.Props -->
        <Dependency Name="DependencyA" Version="1.2.3-45">
            <Uri>https://github.com/dotnet/arepo</Uri>
            <Sha>23498123740982349182340981234</Sha>
        </Dependency>
        <Dependency Name="DependencyB" Version="1.2.3-45">
            <Uri>https://github.com/dotnet/arepo</Uri>
            <Sha>13242134123412341465</Sha>
        </Dependency>
        <Dependency Name="DependencyC" Version="1.2.3-45">
            <Uri>https://github.com/dotnet/arepo</Uri>
            <Sha>789789789789789789789789</Sha>
        </Dependency>
    </ProductDependencies>

    <!-- Elements contains all toolset dependencies -->
    <ToolsetDependencies>
        <-- Non well-known dependency.  Expressed in Version.props -->
        <Dependency Name="DependencyB" Version="2.100.3-1234">
            <Uri>https://github.com/dotnet/atoolsrepo</Uri>
            <Sha>203409823586523490823498234</Sha>
            <Expression>VersionProps</Expression>
        </Dependency>
        <-- Well-known dependency.  Expressed in global.json -->
        <Dependency Name="DotNetSdkVersion" Version="2.200.0">
            <Uri>https://github.com/dotnet/cli</Uri>
            <Sha>1234123412341234</Sha>
        </Dependency>
        <-- Well-known dependency.  Expressed in global.json -->
        <Dependency Name="Arcade.Sdk" Version="1.0.0">
            <Uri>https://github.com/dotnet/arcade</Uri>
            <Sha>132412342341234234</Sha>
        </Dependency>
    </ToolsetDependencies>
</Dependencies>
```

####  eng\Versions.props
```xml
<Project>
  <PropertyGroup>
    <!-- DependencyA, DependencyB, DependencyC substrings correspond to
         DependencyName elements in Version.Details.xml file -->
    <DependencyAPackageVersion>4.5.0-preview2-26403-05</DependencyAPackageVersion>
    <DependencyBPackageVersion>4.5.0-preview2-26403-05</DependencyBPackageVersion>
    <DependencyCPackageVersion>4.5.0-preview2-26403-05</DependencyCPackageVersion>
    ...
  </PropertyGroup>
</Project>
```

####  global.json
```
{
  "sdk": {
    "version": "2.200.0"
  },
  "msbuild-sdks": {
    "Arcade.Sdk": "1.0.0"
  }
}
```

For more information on dependencies please check [DependencyDescriptionFormat](DependencyDescriptionFormat.md)

## The DependencyItem object

When dealing with version/dependency files, Darc will parse information in the version/dependency files to DependencyItem objects.

The `DependencyItem` is composed by `Name`, `Version`, `RepoUri`, `Sha` and `Type`.

## Dependency operations

The commands/operations that Darc can perform against version/dependency files is:

## get

Query for a collection of DependencyItems, based on a set of query-parameters including SHAs, repositories, versions, binaries 
and dependency names.

### Usage

`darc get <options> <query-parameters>`

### options

*  -d, --depend-on: returns the DependencyItems which depend on the `<query-parameters>`. Optional.
*  -p, --produced: return the dependencies that were produced by the `<query-parameters>`. If not set, the returned collection 
will include dependencies where the `<query-parameters>` were used. Optional
*  -l, --latest: return the newest DependencyItems matching the `<query-parameters>`. Optional
*  --remote: if set, Darc will query the reporting store instead of local files. Optional.

### query-parameters

*  "": returns all DependencyItems from the local repo's `version.details.xml`
    *  Example: `darc get`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://github.com/dotnet/arepo",
		sha: "13242134123412341465",
		type: "product"
	},
	{
		name: "DotNetSdkVersion",
		version: "2.200.0",
		repo-uri: "https://github.com/dotnet/cli",
		sha: "1234123412341234",
		type: "toolset"
	},
      ...
]
```
*  `[-s,--sha] <sha> [[-r, --repo-uri] <repo-uri>]`: --repo-uri supports any git repository uri. If --repo-uri is not provided returns the 
DependencyItems from the local repo's `version.details.xml` which match `<sha>`. If a --repo-uri is given and is different 
from the local, get the DependencyItems that match the SHA+repo combination from the reporting store. 
    *  Example: `darc get -s 23498123740982349182340981234`
        * Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://github.com/dotnet/arepo",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
   *  Example: `darc get -s 23498123740982349182340981234 -r https://github.com/dotnet/coreclr`
        * Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://github.com/dotnet/coreclr",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
*  `[-r, --repo-uri] <repo-uri>`: returns the DependencyItems matching the --repo-uri.
    *  Example: `darc get --repo-uri https://github.com/dotnet/corefx`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://github.com/dotnet/corefx",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
*  `[-b, --branch] <branch>`: returns the DependencyItems matching the --branch. This is a `--remote` only command.
    *  Example: `darc get --repo-uri https://github.com/dotnet/corefx -b master`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://github.com/dotnet/corefx",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
*  `[-v, --version] <item-version>`: returns the DependencyItems matching `<item-version>`.
    *  Example: `darc get -v 1.2.3`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3",
		repo-uri: "https://github.com/dotnet/coreclr",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
*  `[-n, --name] <name>`: returns the DependencyItems matching a versioned item name. The use of wildcards is allowed
    *  Example: `darc get --name Dependency?`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repo-uri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
		sha: "13242134123412341465",
		type: "product"
	},
	{
		name: "DependencyB",
		version: "2.200.0",
		repo-uri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreCLR-Trusted",
		sha: "1234123412341234",
		type: "toolset"
	},
	  ...
]
```
*  `[-a, --asset] <asset-name>`: returns the DependencyItems which participated in building the binary/asset matching 
`--asset`. This queries the reporting store.
    *  Example: `darc get --asset Microsoft.DotNet.Build.Tasks.Feed.1.0.0-prerelease-02201-02.nupkg`
	*  Output Sample: 
```
[
	{
		name: "Microsoft.DotNet.Build.Tasks.Feed",
		version: "1.0.0-prerelease-02201-02",
		repo-uri: "https://github.com/dotnet/buildtools",
		sha: "23498123740982349182340981234",
		type: "toolset"
	}
]
```
* We can also combine any of the query-parameters to get a reduced set of results:
    *  Example: `darc get --remote --name Microsoft.DotNet.Build.Tasks.Feed -v 1.0.0-prerelease-02201-02`
	*  Output Sample: 
```
[
	{
		name: "Microsoft.DotNet.Build.Tasks.Feed",
		version: "1.0.0-prerelease-02201-02",
		repo-uri: "https://github.com/dotnet/buildtools",
		sha: "23498123740982349182340981234",
		type: "toolset"
	}
]
```
    
The returned collection will be a result of an AND join of the `<query-parameters>`.

## add

Adds a new dependency to `version.details.xml` and in `version.props` and `global.json` if needed.

## Usage

`darc add <inputs>`

### inputs

*  `[-n, --name] <name>`: the versioned item name. Required.
*  `[-v, --version] <item-version>`: item's version. Required.
*  `[-r, --repo-uri] <repo-uri>`: repo where the item is built. Optional, if not provided this will be fetched from the reporting store.
*  `[-s,--sha] <sha>`: the SHA which is built into this item. Optional, if not provided this will be fetched from the reporting store.

### Example

`darc add -n Microsoft.DotNet.Build.Tasks.Feed -v 1.0.1 -r https://github.com/dotnet/buildtools -s 23498123740982349182340981234`

Output: `true` if succeeded, `false` if otherwise.

## put

Updates a dependency in `version.details.xml` and in `version.props` and `global.json` if needed. 

### Usage

`darc put <inputs>`

### inputs

The name and at least one input are required so we know what dependency to update with which value.

*  `[-n, --name] <name>`: the versioned item name
*  `[-v, --version] <item-version>`: item's version
*  `[-r, --repo-uri] <repo-uri>`: repo where the item is built
*  `[-s,--sha] <sha>`: the SHA which is built into this item

### Example

`darc put -n Microsoft.DotNet.Build.Tasks.Feed -s 23498123740982349182340981234`

Output: `true` if succeeded, `false` if otherwise.

## remove

Removes a dependency from `version.details.xml` matching a versioned item name and an optional version. If only the name is
given all matching dependencies are removed. If version is also provided only the name+version matched dependencies are deleted.

### Usage

`darc remove <inputs>`

### inputs

`--name` is required, `--version` is optional

*  `[-n, --name] <name>`: the versioned item name
*  `[-v, --version] <item-version>`: item's version

### Example

`darc remove -n Microsoft.DotNet.Build.Tasks.Feed`

Output: `true` if succeeded, `false` if otherwise.

## Non-dependency operation (find a better name)

Darc can also perform operations which don't apply to dependencies. The list of these operation will grow as we start using Darc
and at this point it includes:

## push

Creates a PR that could include version/dependency files or a collection of files passed in targetting the specified repo+branch
collection.

### Usage

`darc push <inputs>`

### inputs

*  `[-d, --dependencies]`: if this is specified the PR will only include version/dependency files and will be created only in the repo+branch
where the command was executed. Optional
*  `[[-r, --repo-uris] <path-to-repos-file>]`: If --repo-uris is set, we'll create the PR in the repo+branches defined in it including all the files defined
in FileMapping. When a file is not modified git won't include it in the PR since there are no deltas. If it is not defined Darc will do the same
but using what is defined in Maestro++'s subscriptions. Optional
*  `[-t, --token]`: GitHub's personal access token. Required

### ReposFile.xml example

```xml
<Repositories>
  <Repository Uri=”https://github.com/dotnet/cli”>
    <Branch Name="master">
      <FileMapping>
        <File Source="build.sh" />
        <File Source="eng\common\build.ps1" Destination="eng\common\build\build_new.ps1" />
        <File Source="eng\common\native\*.*" Destination="eng\common\native\nested\*.*" />
      </FileMapping>
    </Branch>
    <Branch Name="rel/1.0.0">
      <FileMapping>
	  	...
      </FileMapping>
    </Branch>
  </Repository>
  <Repository Uri=”https://github.com/dotnet/corefx”>
		...
  </Repository>
</Repositories>
```

File `Source` is the location of the file from which we'll take the contents from to include in the PR. File `Destination` is an optional property 
that if set, determines the location of the file(s) to update in a repo+branch. While renaming the files is not a common scenario, this is supported
by using a different name from that in `Source`. If `Destination` is not set, the `Source` value will be used as default.

### Example

*  `darc push -t 123f1234ed123ccc123f236e12b1234a456b987`
*  `darc push -d -t 123f1234ed123ccc123f236e12b1234a456b987`
*  `darc push -r "E:\myrepos.xml" -t 123f1234ed123ccc123f236e12b1234a456b987`

Output: 
```
[
	{
		repo-uri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
		branch: "master",
		prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88066"
	},
	{
		repo-uri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
		branch: "release/2.1",
		prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88067"
	},
	{
		repo-uri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
		branch: "release/1.1.0",
		prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88067"
	},
	...
]
```
