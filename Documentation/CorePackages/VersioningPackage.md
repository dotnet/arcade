# Arcade SDK Versioning Package

The Arcade SDK implements [here](../src/Microsoft.DotNet.Arcade.Sdk/tools/Version.targets) the Versioning proposal explained in the [.NET Core Ecosystem v2 - Versioning](VersioningSchema.md) document. This document provides a quick description of how to use the package and how it works. More documentation is available in the package's .target/.props file.

Arcade onboarded repos use this package automatically by using the Arcade SDK. *If you use the SDK you won't have to do anything else to use the format strings described in the versioning proposal.* 

The main entry point for the package is the `_InitializeAssemblyVersion` target that executes before the `GetAssemblyVersion`.  Below is a list of the main parameters that control the logic.

## Parameters

| Parameter                  | Description                                                  |
| -------------------------- | ------------------------------------------------------------ |
| SemanticVersioningV1       | Specify whether the version string should be in SemVer 1.0 format or not. |
| DotNetUseShippingVersions  | Set to `true` to produce shipping version strings in non-official builds. I.e., instead of fixed values like "42.42.42.42" for AssemblyVersion. |
| OfficialBuild              | Boolean indicating if the current build is official or not.  |
| OfficialBuildId            | ID of current build. The accepted format is "yyyyMMdd.r" if empty it will be computed following the logic explained in the [Versioning schema](VersioningSchema.md). |
| ContinuousIntegrationBuild | Specify whether the build is happening on a CI server (PR build or official build). |
| DotNetFinalVersionKind     | Specify the "kind" of version: "release", "prerelease" or "" |
| PreReleaseVersionLabel     | Pre-release label to be used on the string. E.g., "ci", "dev", "beta", etc. |
| VersionPrefix              | The leading part of the version string. If empty the tool will try to compute it based on `$(MajorVersion).$(MinorVersion).0` |

## Output

This is the list of output properties from the Versioning package.

| Name            | Description                                                  |
| --------------- | ------------------------------------------------------------ |
| PackageVersion  | Append short commit SHA to PackageVersion. SemanticVersioningV1 controls the separator between the two fields. |
| AssemblyVersion | Set to "42.42.42.42" if empty and VersionSuffixDateStamp is also empty, otherwise leave as is. |
| FileVersion     | FileVersion string. If "VersionSuffixDateStamp" is empty FileVersion will be set to "42.42.42.42424". |
| VersionPrefix   | The leading part of the version string as specified in the Versioning schema. |
| VersionSuffix   | The suffix part of the version string, including the pre-release portion. |





	











