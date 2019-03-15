# .NET Core Ecosystem v2 - Versioning

| Date       | Note                                                         |
| ---------- | ------------------------------------------------------------ |
| 10/03/2018 | The implementation doesn't include the SHA on the version strings anymore, because: 1) The chaining of targets to plug that into SemVer1 strings were causing generation of incorrect NuSpec files and no clear path to fix it was found and, 2) VS CoreXT build system doesn't support it. |

## Versioning Plan

.NET Core repositories should use [SemVer2](https://semver.org) for their asset versioning scheme. Package versions take the general form of:

```
MAJOR.MINOR.PATCH-PRERELEASE+BUILDMETADATA
```

MAJOR, MINOR, and PATCH versions are rigid in their requirements.  Please refer to the [SemVer2 documentation](https://semver.org) for details. PRERELEASE and BUILD are optional and leave a fair bit of room for organizations to implement what they want. The only caveat is that build metadata cannot be used to differentiate two different packages. Build metadata should not be used when determining version precedence. There are two primary questions:
- What goes in the PRERELEASE and BUILD fields and when are they used?
- Should we use date agnostic versioning vs. date varying versioning?

## Date Agnostic vs. Date-Varying Build Versioning

In the context of the .NET Core product, a build version number is date agnostic if successive builds of the same commit produce the same build version numbers. Historically, .NET Core has had repositories that implement date agnostic versioning through build numbers that increment based on commit depth, as well as repositories that base their build number off of the date and number of builds prior on the day (date based versioning).

Both types of versioning have advantages and disadvantages:
### Date Agnostic Versioning
**PROS:**
- Assets with identical version metadata can be reproduced without outside input (e.g. providing a build id)
- Parallel, independent legs of the same build do not require parent orchestration and will generate coherent versioning.  Separate parts of the build can potentially be respun as required.
- We already have 'date agnostic' versioning at the end of a product cycle.
- Build metadata typically includes SHA, which makes identification of the source of bits easy.

**CONS:**
- Some related infrastructure systems may not support the ingestion of multiple assets with the same name/version.  For example, MyGet, NuGet, or VSTS support overwriting to varying degrees. You're left to deal with these scenarios on a case-by-case basis.  For example:
  - Introduce a new meaningless commit to bump the version number
  - Successive builds of the same SHA, if they were to have different outputs (e.g. a checked vs. debug build) may require a clean of the cache.
  - Temporarily unlock a feed to enable re-publishing (may not be possible, and bad practice)
  - Add logic into all processes to gracefully understand and handle overwrite cases (e.g. are bits the same? then okay)
  - Telemetry systems must understand reruns of the same build.
- Most of the .NET Core builds aren't bit-for-bit identical at the same commit given the same inputs.  If they were, then a partial-respin can gracefully handle dealing with overwrites.
- Date agnostic version doesn't encode inputs (e.g. checked build vs. release build).  This can make it tricky to run 'non-standard' build scenarios if they interact with external systems.
- Generating the same package version in multiple builds with different outputs bits means that package caches must be cleared.

### Date-Varying Versioning
**PROS:**
- Respins are easier.  No overwriting except for release versions at the end of the product cycle.  Release versions at the end of the cycle typically avoid external overwrite-averse systems.
- Non-standard builds (e.g. different input parameters) do not collide with standard builds at the same SHA when dealing with external systems.
- File versions must be ever increasing to correctly layout new files for servicing (MSI requirement)
- Does not affect the determinism of the build. Input dates are just another build parameter (e.g. OfficialBuildId) and rerunning a build with the same input date should produce equivalent binaries.

**CONS:**
- Requires orchestration to produce a coherent set of versions across multiple build legs
- More difficult to reproduce version identical bits (need to know input parameters)
- Source SHA not easily identifiable based on asset version.

### Conclusion

Date agnostic versioning is more hassle than it's worth, though having the SHA in the output version number is also useful.  We should combine a SHA in the build metadata with the build date+revision (short data + number of builds so far today) to generate a date-varying, unique, identifiable build.

## Build Determinism

  There is often concern around build determinism when date-varying versioning is used. It is important to note that date-varying versioning does not affect the ability to have deterministic builds in either the local dev or official build lab scenarios.  The date is either a provided parameter or obtained from git information, meaning that setting it to a specific value at a specific commit can enable the production of the same outputs over and over again.  Date varying versioning only says that **by default** this input varies from build to build.

## Version parts specified by repository

The repository specifies a 3-part version prefix in `VersionPrefix` build property (`MAJOR.MINOR.PATCH`), or 2 parts using `MajorVersion` and `MinorVersion` properties (the patch number defaults to 0 in such case). 

The versioning scheme defined below imposes the following limits on these version parts:
- `MAJOR` version is in range [0-65535]
- `MINOR` version is in range [0-654]
- `PATCH` version is in range [0-9999]

## Package Version Fields

- **MAJOR** - Major version
- **MINOR** - Minor version
- **PATCH** - Patch version
- **PRERELEASE** - Prerelease label
- **REVISION** - Number of official builds during the current day
- **SHORTDATE** - 5 digit date

**Note that version fields should not be zero-padded**

## Package Version Kinds

Package versions come in the following kinds, depending on the point in the product cycle:

- **Local developer build default** 
  
  ```
  MAJOR.MINOR.PATCH-dev
  ```
  Example:
  ```
    1.0.0-dev+abcdef
  ```

- **PR validation build** 
  
  ```
  MAJOR.MINOR.PATCH-ci
  ```
  Example:
  ```
    1.0.0-ci+abcdef
  ```
  
- **Daily official build** 

  ```
  MAJOR.MINOR.PATCH-PRERELEASE.SHORTDATE.REVISION
  ```
  Example:
  ```
    1.0.0-preview1.25405.1+abcdef
  ```
  
- **Final pre-release build** 
  
   Versions should include **MAJOR**, **MINOR**, **PATCH**, and **PRERELEASE** tag but no **SHORTDATE**, **REVISION**.  **PRERELEASE** should be suffixed with `'.final'`.  This avoids a common issue in NuGet package resolution where `2.1.0-rc1` < `2.1.0-rc1.12345`. The intention is that the final build is resolved over the date-versioned build.
   
  ```
  MAJOR.MINOR.PATCH-PRERELEASE.final
  ```
  Example:
  ```
    1.0.0-preview1.final
  ```

- **Release build** 

  Versions should include **MAJOR**, **MINOR**, **PATCH**.
  
  ```
  MAJOR.MINOR.PATCH
  ```
  Example:
  ```
    1.0.0
  ```

The format of package versions produced by the build is determined based on the value of variable `DotNetFinalVersionKind`:

| DotNetFinalVersionKind   | examples                    |
|--------------------------|-----------------------------|
| ""                       | "1.2.3-dev", "1.2.3-ci", "1.2.3-beta.12345.1" |
| "prerelease"             | "1.2.3-beta.final"   |
| "release"                | "1.2.3"                     |

## Package Version Generation

- **MAJOR**, **MINOR**, **PATCH**:
  Specified in source using `VersionPrefix` .NET Core SDK property, defaults to `1.0.0`.

- **PRERELEASE**: 
  Property `PreReleaseVersionLabel` specifies the label for an official build.
  Label `dev` is used for developer build and `ci` for PR validation build.
  
- **REVISION**, **SHORTDATE**: 
  - In official builds the values are derived from build parameter `OfficialBuildId` with format `20yymmdd.r` like so:
    - REVISION is set to `r` component of `OfficialBuildId`
    - SHORTDATE is set to `yy` * 1000 + 50 * `mm` + `dd`. In year 2018 the value is in range [18051, 18631].
  - In CI and local dev builds REVISION and SHORTDATE are not included in the package version,
    unless `DotNetUseShippingVersion` is `true`, in which case the values of `yy`, `mm` and `dd` are derived from the current date
    and `r` = 1.

## File Version Generation

File version has 4 parts and need to increase every official build. This is especially important when building MSIs. 

```
FILEMAJOR.FILEMINOR.FILEPATCH.FILEREVISION
```

- **FILEMAJOR**: 
  Set to `MAJOR`.
- **FILEMINOR**:
  Set to `MINOR` * 100 + `PATCH` / 100.
- **FILEPATCH**:
  Set to (PATCH % 100) * 100 + `yy`.
- **FILEREVISION**:
  Set to (50 * `mm` + `dd`) * 100 + `r`. This algorithm makes it easy to parse the month and date from FILEREVISION while staying in the range of a short which is what a version element uses.

The values of `yy`, `mm`, `dd`, and `r` are derived from `OfficialBuildId` or the current date (same as when calculating Package Version).

## SemVer1 Fallback

In cases where SemVer2 cannot be used (e.g. old versions of NuGet), we can fall back to [SemVer1](https://semver.org/spec/v1.0.0.html).  In SemVer1, there is no built in build metadata, and the pre-release field may only contain [0-9A-Za-z-].  To comply, cases where + or . are used in SemVer2's prerelease field are replaced with -.

The repository opts into SemVer1 fallback by setting `SemanticVersioningV1` property to `true`.

Examples of SemVer1 package versions:

| DotNetFinalVersionKind   | examples                     |
|--------------------------|-----------------------------|
| ""                       | "1.2.3-dev", "1.2.3-ci", "1.2.3-beta-12345-01"|
| "prerelease"             | "1.2.3-beta-final"   |
| "release"                | "1.2.3"                     |

Note that the REVISION number is zero-padded to two characters.

## Assembly Informational Version Generation

Assembly informational version is generated by .NET SDK. If the project references SourceLink package (added by Arcade SDK by default) the assembly informational version will include the full commit SHA. An example of a version string that the assembly will include:

```csharp
[AssemblyInformationalVersion("1.2.3-beta.12345.1+fe80f83075d723eddd6e26582c75f27f242c69c4")]
```

## NuGet Package Repository Information

Packages produced by projects that reference SourceLink package and use standard NuGet Pack target for packaging will include repository URL and commit SHA metadata in their nuspec files:

```xml
<repository type="git" url="https://github.com/dotnet/roslyn" commit="d37ac834f69e1a771626813da3b820502309462d" />
```

## Implementation

The Arcade SDK implements [here](../../src/Microsoft.DotNet.Arcade.Sdk/tools/Version.targets) the Versioning schema described in this document. This section provides a brief outline of how to use the implementation and how it works. More documentation is available in the respective [.target/.props](../../src/Microsoft.DotNet.Arcade.Sdk/tools/Version.targets) file.

Arcade onboarded repos use this implementation automatically by using the Arcade SDK. *If you use the SDK you won't have to do anything else to use the format strings described in the proposal.* 

Below is a list of the main parameters that control the logic.

### Parameters

| Parameter                  | Scope  | Description                                                  |
| -------------------------- | ------ | ------------------------------------------------------------ |
| OfficialBuildId            | Arcade | ID of current build. The accepted format is `yyyyMMdd.r`. Should be passed to build in YAML official build defintion. |
| SemanticVersioningV1       | Arcade | Set to `true` in `Versions.props` file to use versions compatible with SemVer 1.0. |
| DotNetUseShippingVersions  | Arcade | Set to `true` to produce shipping version strings in non-official builds. I.e., instead of fixed values like `42.42.42.42` for `AssemblyVersion`. |
| DotNetFinalVersionKind     | Arcade | Specify the kind of version being generated: `release`, `prerelease` or empty. |
| PreReleaseVersionLabel     | Arcade | Pre-release label to be used on the string. E.g., `beta`, `prerelease`, etc. `ci` and `dev` are reserved for non-official CI builds and dev builds, respectively. |
| VersionPrefix              | .NET   | Specity the leading part of the version string. If empty and both `MajorVersion` and `MinorVersion` are set, initialized to `$(MajorVersion).$(MinorVersion).0`. |
| MajorVersion               | Arcade | Major version to use in `VersionPrefix`. |
| MinorVersion               | Arcade | Minor version to use in `VersionPrefix`. |
| ContinuousIntegrationBuild | .NET   | Specify whether the build is happening on a CI server (PR build or official build). |

## Output
 This is the list of properties set by Arcade SDK versioning implementation. The values are available only after `GetAssemblyVersion` target has been executed.

| Name            | Scope | Description                                                  |
| --------------- | ----- | ------------------------------------------------------------ |
| OfficialBuild   | Arcade| Boolean indicating that the current build is official. Set to `true` if `OfficialBuildId` is non-empty. |
| AssemblyVersion | .NET  | Set to `42.42.42.42` if not set in the project, the build is not official and `DotNetUseShippingVersions` is not `true`. |
| FileVersion     | .NET  | Set to `42.42.42.42424` if the build is not official and `DotNetUseShippingVersions` is not `true`. |
| VersionPrefix   | .NET  | Initialized to `$(MajorVersion).$(MinorVersion).0` if not set by the project.  |
| VersionSuffix   | .NET  | The suffix part of the version string, including the pre-release portion. |

