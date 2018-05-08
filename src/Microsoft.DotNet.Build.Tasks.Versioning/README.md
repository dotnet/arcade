# Microsoft.DotNet.Build.Tasks.Versioning

Task package which handles generation of version strings. The versioning format that we use is described [here](../../Documentation/Versioning). See ["Task Packages"](../../Documentation/TaskPackages.md#usage) for guidance on installing this package.

Two files are provided. One with the default values for the versioning, called Versioning.props, and another file with the targets, called Versioning.targets.

Targets in this package:

 - Versioning

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
Builds          | string      | Number of builds for current date.
ShortSha        | string      | SHA of the repo last commit.
FormatName      | string      | The name of the format string you want to use. Options are "dev", "final-prerelease" and "stable".
IncludeSha      | boolean     | Whether or not the repo SHA should be included in the version string. Default to false.
IncludeDate     | boolean     | Whether or not the build date should be included in the version string. Default to false.
VersionString   | string      | **Output** Version string produced.
VersionSeedDate         | string | Still work in progres.
OfficialBuildId         | string | Still work in progres.
VersionComparisonDate   | string | Still work in progres.
VersionPadding          | string | Still work in progres.

#### Usage

To use this target override the needed parameters (see list above) and call the target:

```xml
  <PropertyGroup>
    <Major>1</Major>
    <Minor>2</Minor>
    <Patch>3</Patch>
    <FormatName>stable</FormatName>
  </PropertyGroup>

  <Target Name="Build" DependsOnTargets="Versioning">
    <Message Text="Building version: $(VersionString)" /> 
    <!-- Should produce: Building version: 1.2.3-final" -->
  </Target>
```


## Tasks

### `GenerateVersioningDate`

TBD
