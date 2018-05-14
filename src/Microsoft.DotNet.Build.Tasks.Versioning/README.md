# Microsoft.DotNet.Build.Tasks.Versioning

Task package which handles generation of version strings. The versioning format that we use is described [here](../../Documentation/Versioning). See ["Task Packages"](../../Documentation/TaskPackages.md#usage) for guidance on installing this package.

Two files are provided. One with the default values for the versioning, called Versioning.props, and another file with the targets, called Versioning.targets.

Targets in this package:

 - Versioning
 - CreateVersioningCacheFile
 - DeleteVersioningCacheFile

Tasks in this package:

 - GenerateVersioningDate


## Targets

### `Versioning`

Returns a version string following the guidelines documented [here](../../Documentation/Versioning). 

#### Parameters

Property        | Type        | Description
----------------|-------------|--------------------------------------------------------------------------------
Major           | string      | Major version number. Defaults to 1.
Minor           | string      | Minor version number. Defaults to 0.
Patch           | string      | Patch number. Default to 0.
Prerelease      | string      | Prerelease label. E.g. "beta", "preview", etc.
ShortDate       | string      | Date to be used in the version string.
Revision        | string      | Number of Revision for current date.
ShortSha        | string      | SHA of the repo last commit.
FormatName      | string      | The name of the format string you want to use. Options are "dev", "final-prerelease" and "stable".
IncludeSha      | boolean     | Whether or not the repo SHA should be included in the version string. Default to false.
IncludeDate     | boolean     | Whether or not the build date should be included in the version string. Default to false.
SemVerOne       | boolean     | Whether to go back to Semantic Versioning 1.0. Default to false.
Padding         | integer     | Minimum size of the date field in the versioning string. The field will be padded with leading zeros.
VersionString   | string      | **Output** Version string produced.
VersionSeedDate         | date | Date of the current build. Format is: yyyy-MM-dd
OfficialBuildId         | string | Optional parameter containing an Official Build Id. When informed, the revision number and short date will be extracted from it. Format is: (yyyyMMdd)[-.]([0-9]+)
VersionComparisonDate   | string | Optional parameter containing base date for calculating the ShortDate string. Format: yyyy-MM-dd. Default is: 1996-04-01


#### Usage

To use this target override the needed parameters (see list above) and call the target:

```xml
  <PropertyGroup>
    <!-- Specify that a "dev" format string should be generated. -->
    <FormatName>dev</FormatName>

    <!-- Input version numbers. -->
    <Major>1</Major>
    <Minor>2</Minor>
    <Patch>3</Patch>
    <Revision>42</Revision>

    <!-- The shortdate field should be included in the string. -->
    <!-- The content of the ShortDate field is used "as-is" - no formatting is applied. -->
    <IncludeDate>true</IncludeDate>
    <ShortDate>514</ShortDate>

    <!-- Label appended after the initial version numbers. -->
    <PreRelease>prev1</PreRelease>

    <!-- SHA of the repository commit should be included in the string. -->
    <!-- The content of the ShortSha field is used "as-is" - no formatting is applied. -->
    <IncludeSha>true</IncludeSha>
    <ShortSha>badec0</ShortSha>
  </PropertyGroup>

  <Target Name="Build" DependsOnTargets="Versioning">
    <Message Text="Building version: $(VersionString)" /> 
    <!-- Should produce: Building version: 1.2.3-prev1.514.42+badec0" -->
  </Target>
```

### `CreateVersioningCacheFile`

Create a cache file called VersioningCache.props at the path pointed by $(BaseIntermediateOutputPath).

#### Parameters

Property        | Type        | Description
----------------|-------------|--------------------------------------------------------------------------------
ShortDate       | string      | Date to be used in the version string.
Revision        | string      | Number of Revision for current date.
ShortSha        | string      | SHA of the repo last commit.
VersioningCacheFile | string | Path to the cache file. Includes name and extension.


### `DeleteVersioningCacheFile`

Delete the cache file VersioningCache.props at the path pointed by $(BaseIntermediateOutputPath).

#### Parameters

Property        | Type        | Description
----------------|-------------|--------------------------------------------------------------------------------
VersioningCacheFile | string | Path to the cache file. Includes name and extension.


## Tasks

### `GenerateVersioningDate`

Task used to generate a ShortDate string and Revision number given input parameters.
If an OfficialBuildId is specified the ShortDate and Revision are extracted and returned from it.
Otherwise, ShortDate is calculated as the concatenation of number of months between 
SeedDate and ComparisonDate with the day informed in SeedDate.

#### Parameters

Property            | Type        | Description
--------------------|-------------|--------------------------------------------------------------------------------
SeedDate            | date        | Date of the current build. Format is: yyyy-MM-dd
OfficialBuildId     | string      | Optional parameter containing an Official Build Id. When informed, the revision number and short date will be extracted from it. Format is: (yyyyMMdd)[-.]([0-9]+)
IncludePadding      | boolean     | If the ShortDate field should be padded with zeros to match the size specified in Padding.
Padding             | integer     | Minimum size of the date field in the versioning string. The field will be padded with leading zeros.
ComparisonDate      | string      | Optional parameter containing base date for calculating the ShortDate string. Format: yyyy-MM-dd. Default is: 1996-04-01
GeneratedShortDate  | string      | **Output** Generated short date.
GeneratedRevision   | string      | **Output** Generated revision number.

#### Usage

```xml
    <GenerateVersioningDate SeedDate="$(VersionSeedDate)" OfficialBuildId="$(OfficialBuildId)" ComparisonDate="$(VersionComparisonDate)" IncludePadding="$(ShouldSupportSemVerOne)" Padding="$(Padding)">
      <Output TaskParameter="GeneratedRevision" PropertyName="Revision"  />
      <Output TaskParameter="GeneratedShortDate" PropertyName="ShortDate"  />
    </GenerateVersioningDate>
```
