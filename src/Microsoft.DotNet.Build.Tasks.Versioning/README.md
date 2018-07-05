# Microsoft.DotNet.Build.Tasks.Versioning

Task package that handles generation of version strings. The versioning format that we use is described [here](../../Documentation/Versioning).
Two files are provided in the build folder. One for linking the library and setting default values, called `Microsoft.DotNet.Build.Tasks.Versioning.props`, and another, called `Microsoft.DotNet.Build.Tasks.Versioning.targets` with the targets definitions.

Targets in this package:

 - GenerateVersionString

Tasks in this package:

 - GenerateVersionComponents

## Targets

### `GenerateVersionString`

This target is registered to run before the `GetAssemblyVersion` target. It's purpose is to construct
the versioning string following the plan outlined [here](../../Documentation/Versioning). As a result, at
the beginning of the `GetAssemblyVersion` target execution a property called `Version` would be set with
the value of the package version being built.

#### Parameters

Property                | Type        | Description
------------------------|-------------|--------------------------------------------------------------------------------
VersionPrefix           | string      | Prefix of the version string. Default is 1.0.0
PB_VersionStamp         | string      | Prerelease label. E.g. "beta", "preview", etc.
PB_IsStable             | boolean     | Indicates if this is a stable (true) or unstable build (false).
OfficialBuildId         | string      | Optional parameter containing an Official Build Id. When informed, the revision number and short date will be extracted from it. Format is: (yyyyMMdd)\[-.\]([0-9]+). By default the `BUILD_BUILDNUMBER` environment variable is used.
SemanticVersioningV1    | boolean     | Whether to use Semantic Versioning 1.0. That would result in using padding on Revision and ShortDate fields and adjusting pre-release fields separators to `-`. Defaults to false.
BaselineDate            | string      | Optional parameter containing base date for calculating the ShortDate string. Format: yyyy-MM-dd. Default is: 1996-04-01
Version                 | string      | **Output** Version string produced.
PackageVersion          | string      | **Output** Same as Version field.
VersionSuffix           | string      | **Output** Only the suffix of the version string. I.e., the part after Major.Minor.Patch.
FileVersion             | string      | **Output** File version produced in the format Major.Minor.ShortDate.Revision

