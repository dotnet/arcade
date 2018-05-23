# Darc

## Description

Darc is meant to be the **only** way we interact and alter version/dependency files as well as bootstraping files and scripts
in a repo.

Darc's operations range from performing CRUD on version/dependency files to creating PRs in specified repos
as well as building parts or the whole product with changes in the local repo where the command is executed

## version/dependency files

The version/dependency files define a set of versioned items which the repo depends on. These files are:

*  eng\version.details.xml
*  eng\version.props
*  global.json

For more information on dependencies please check [DependencyDescriptionFormat](DependencyDescriptionFormat.md)

## The DependencyItem object

When executing CRUD operations, Darc will parse information in the version/dependency files to DependencyItem objects.

The DependencyItem is defined as follows:

```
public class DependencyItem
{
	public string Name;
	public string Version;
	public string Repo;
	public string Sha;
	public string Location;
}
```

## Dependency operations

The commands/operations that Darc can perform against version/dependency files is:

## get

Query for a collection of DependencyItems, based on a set of query-parameters including
shas, repositories, versions, binaries and dependency names.

### Usage

darc get \<options> \<query-parameters>

### options

*  -p, --produced: return the dependencies that were produced by the \<query-parameters>. If not set,
                the returned collection will include dependencies where the \<query-parameters> were 
                used.
*  -rm, --remote: data source is the reporting store

### query-parameters

*  "": returns all DependencyItems from the local repo's `Version.Details.xml`
    *  Example: darc get
*  [-s,--sha] \<sha> [[-r, --repo] \<repo>]: if \<repo> is not 
provided returns the DependencyItems from the local repo's `Version.Details.xml` which match \<sha>. If
a \<repo> is given and is different from the local, get the DependencyItems that match
the sha+repo combination from the reporting store. 
    *  Example: darc get -s 23498123740982349182340981234
    *  Example: darc get -s 23498123740982349182340981234 -r dotnet/coreclr
*  [-r, --repo] \<repo>: if \<repo> is different from local returns the DependencyItems matching the \<repo>
from the reporting store, if same, return the the collection from `Version.Details.xml`
    *  Example: darc get --repo dotnet/corefx
*  [-v, --version] \<item-version>: returns the DependencyItems matching \<item-version>.
    *  Example: darc get -v 1.2.3
*  [-n, --name] \<name>: returns the DependencyItems matching a versioned item name.
    *  Example: darc get --name MyDependency
*  [-b, --binary] \<binary-name>: returns the DependencyItems matching a binary/asset name.
    *  Example: darc get --binary Microsoft.DotNet.Build.Tasks.Feed.1.0.0-prerelease-02201-02.nupkg
* We can also combine any of the query-parameters to get a reduced set of results:
    *  Example: darc get -rm --name Microsoft.DotNet.Build.Tasks.Feed -v 1.0.0-prerelease-02201-02
    
The returned collection will be a result of an AND join of the \<query-parameters>.

## put

Add/update a dependency in `Version.Details.xml` (and in `Version.props` and `global.json` if needed). 
If the dependency name passed as an input already exists in `Version.Details.xml` we'll update the entry with the 
rest of the inputs. If it doesn't exist a new one is created.

### Usage

darc put \<inputs>

### inputs

All the following inputs are required (probably except location depending on the discussion).

*  [-n, --name] \<name>: the versioned item name
*  [-v, --version] \<item-version>: item's version
*  [-r, --repo] \<repo>: repo where the item is built
*  [-s,--sha] \<sha>: the sha which is built into this item
*  [-l, --location]: the location of the built binary (should this be here?)

### Example

darc put -n Microsoft.DotNet.Build.Tasks.Feed -v 1.0.1 -r dotnet/buildtools -s 23498123740982349182340981234

## remove

Removes a dependency from `Version.Details.xml` matching a versioned item name. (Do we want to make this match other
things than just name. i.e remove all dependencies from dotnet/coreclr or all versions 1.2.3? If this is the case
I'll add \<inputs>)

### Usage

darc remove \<name>

### Example

darc remove Microsoft.DotNet.Build.Tasks.Feed

## Non-dependency operation (find a better name)

Darc

Darc can also perform operations which don't apply to dependencies. The list of these operation will grow as we start using Darc
and at this point it includes:

## push

Creates a PR that could include version/dependency files or a collection of files passed in targetting the specified repo+branch
collection.

### Usage

darch push \<inputs>

### inputs

*  [-d, --dependencies]: if this is specified the PR will only include version/dependency files and will be created only in the repo+branch
where the command was executed
*  [[-r, --repos] \<path-to-repos-file>]: If --repos is set, we'll create the PR in the repo+branches defined in it including all the files defined
in FileMapping. When a file is not modified git won't include it in the PR since there are no deltas. If it is not defined Darc will do the same
but using what is defined in Maestro++'s subscriptions
*  [-t, --token]: GitHub's personal access token

### ReposFile.xml example

\<Repositories>
   \<Repository Name=”dotnet/cli”>
		\<Branch Name="master">
			\<FileMapping>
				\<File Origin="build.sh" Destination="build.sh" />
				\<File Origin="eng\common\build.ps1" Destination="eng\common\build\build.ps1" />
				\<File Origin="eng\common\native\*.*" Destination="eng\common\native\*.*" />
			\<FileMapping>
		\</Branch>
		\<Branch Name="rel/1.0.0">
			\<FileMapping>
				...
			\<FileMapping>
		\</Branch>
   \</Repository>
   \<Repository Name=”dotnet/corefx”>
		...
   \</Repository>
\</Repositories>

### Example

*  darc push -t 123f1234ed123ccc123f236e12b1234a456b987
*  darc push -d -t 123f1234ed123ccc123f236e12b1234a456b987
*  darc push -r "E:\myrepos.xml" -t 123f1234ed123ccc123f236e12b1234a456b987

## build

TBD



