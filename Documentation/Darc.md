# Darc

## Description

*  Darc is meant to be the **only** way developers and other tools like Maestro++ interact and alter version/dependency files 
as well as bootstrapping files and scripts in arcade participating repos.
*  Darc's operations range from altering version/dependency files to creating PRs in specified repos.

## version/dependency files

The version/dependency files define a set of versioned items which the repo depends on. These files are:

#### eng\version.details.xml
```
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

####  eng\version.props
```
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

The `DependencyItem` is composed by `Name`, `Version`, `RepoUri`, `Sha`, `Type` and `Location`.

## Dependency operations

The commands/operations that Darc can perform against version/dependency files is:

## get

Query for a collection of DependencyItems, based on a set of query-parameters including shas, repositories, versions, binaries 
and dependency names.

### Usage

`darc get <options> <query-parameters>`

### options

*  -p, --produced: return the dependencies that were produced by the `<query-parameters>`. If not set, the returned collection 
will include dependencies where the `<query-parameters>` were used.
*  --remote: if set, Darc will query the reporting store instead of local files.

### query-parameters

*  "": returns all DependencyItems from the local repo's `version.details.xml`
    *  Example: `darc get`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repoUri: "https://github.com/dotnet/arepo",
		sha: "13242134123412341465",
		type: "product"
	},
	{
		name: "DotNetSdkVersion",
		version: "2.200.0",
		repoUri: "https://github.com/dotnet/cli",
		sha: "1234123412341234",
		type: "toolset"
	},
      ...
]
```
*  `[-s,--sha] <sha> [[-r, --repoUri] <repoUri>]`: --repoUri supports any git repository uri. If --repoUri is not provided returns the 
DependencyItems from the local repo's `version.details.xml` which match `<sha>`. If a --repoUri is given and is different 
from the local, get the DependencyItems that match the sha+repo combination from the reporting store. 
    *  Example: `darc get -s 23498123740982349182340981234`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repoUri: "https://github.com/dotnet/arepo",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
    *  Example: `darc get -s 23498123740982349182340981234 -r dotnet/coreclr`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repoUri: "https://github.com/dotnet/coreclr",
		sha: "23498123740982349182340981234",
		type: "product"
	}
]
```
*  `[-r, --repo] <repo>`: if --repoUri is different from local returns the DependencyItems matching the --repoUri from the reporting 
store, if same, return the the collection from `version.details.xml`
    *  Example: `darc get --repoUri https://github.com/dotnet/corefx`
	*  Output Sample: 
```
[
	{
		name: "DependencyA",
		version: "1.2.3-45",
		repoUri: "https://github.com/dotnet/corefx",
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
		repoUri: "https://github.com/dotnet/coreclr",
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
		repoUri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
		sha: "13242134123412341465",
		type: "product"
	},
	{
		name: "DependencyB",
		version: "2.200.0",
		repoUri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreCLR-Trusted",
		sha: "1234123412341234",
		type: "toolset"
	},
	  ...
]
```
*  `[-b, --binary] <binary-name>`: returns the DependencyItems matching a binary/asset name. This queries the reporting store.
    *  Example: `darc get --binary Microsoft.DotNet.Build.Tasks.Feed.1.0.0-prerelease-02201-02.nupkg`
	*  Output Sample: 
```
[
	{
		name: "Microsoft.DotNet.Build.Tasks.Feed",
		version: "1.0.0-prerelease-02201-02",
		repoUri: "https://github.com/dotnet/buildtools",
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
		repoUri: "https://github.com/dotnet/buildtools",
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

All the following inputs are required (probably except location depending on the discussion).

*  `[-n, --name] <name>`: the versioned item name
*  `[-v, --version] <item-version>`: item's version
*  `[-r, --repoUri] <repoUri>`: repo where the item is built
*  `[-s,--sha] <sha>`: the sha which is built into this item
*  `[-l, --location]`: the location of the built binary (should this be here?)

### Example

`darc add -n Microsoft.DotNet.Build.Tasks.Feed -v 1.0.1 -r https://github.com/dotnet/buildtools -s 23498123740982349182340981234`

Output: `true` if succeeded, `false` if otherwise.

## put

Updates a dependency in `version.details.xml` and in `version.props` and `global.json` if needed. 

### Usage

`darc put <inputs>`

### inputs

Tha name and at least one input are required so we know what dependency to update with which value.

*  `[-n, --name] <name>`: the versioned item name
*  `[-v, --version] <item-version>`: item's version
*  `[-r, --repoUri] <repoUri>`: repo where the item is built
*  `[-s,--sha] <sha>`: the sha which is built into this item
*  `[-l, --location]`: the location of the built binary (should this be here?)

### Example

`darc put -n Microsoft.DotNet.Build.Tasks.Feed -s 23498123740982349182340981234`

Output: `true` if succeeded, `false` if otherwise.

## remove

Removes a dependency from `version.details.xml` matching a versioned item name. (Do we want to make this match other
things than just name. i.e remove all dependencies from dotnet/coreclr or all versions 1.2.3? If this is the case I'll add `<inputs>`)

### Usage

`darc remove <name>`

### Example

`darc remove Microsoft.DotNet.Build.Tasks.Feed`

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
where the command was executed
*  `[[-r, --repos] <path-to-repos-file>]`: If --repos is set, we'll create the PR in the repo+branches defined in it including all the files defined
in FileMapping. When a file is not modified git won't include it in the PR since there are no deltas. If it is not defined Darc will do the same
but using what is defined in Maestro++'s subscriptions
*  `[-t, --token]`: GitHub's personal access token

### ReposFile.xml example

```
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
  <Repository Name=”dotnet/corefx”>
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
	  repoUri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
	  branch: "master",
	  prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88066"
		},
		{
	  repoUri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
	  branch: "release/2.1",
	  prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88067"
		},
		{
	  repoUri: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/DotNet-CoreFX-Trusted",
	  branch: "release/1.1.0",
	  prLink: "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/Default/_git/DotNet-CoreFX-Trusted/pullrequest/88067"
		},
		...
	]
```

## build

Since the process of building the product or a part of the product is not an straight forward  operation it will be defined in a individual document
and then linked here.