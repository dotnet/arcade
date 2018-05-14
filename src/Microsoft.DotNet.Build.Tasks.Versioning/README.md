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
VersionComparisonDate   | date | Optional parameter containing base date for calculating the ShortDate string. Format: yyyy-MM-dd. Default is: 1996-04-01


#### Usage

To use this target override the needed parameters (see list above) and call the target:

```xml
  <PropertyGroup>
    <Major>1</Major>
    <Minor>2</Minor>
    <Patch>3</Patch>
    
    <FormatName>stable</FormatName>
    <Prerelease>preview1</Prerelease>
  </PropertyGroup>

  <Target Name="Build" DependsOnTargets="Versioning">
    <Message Text="Building version: $(VersionString)" /> 
    <!-- Should produce: Building version: 1.2.3-final" -->
  </Target>
```


## Tasks

### `GenerateVersioningDate`

TBD
