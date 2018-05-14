# .NET Core Ecosystem v2 - Versioning

.NET Core repositories should use [SemVer2](https://semver.org) for their asset versioning scheme. Versions take the general form of:

```MAJOR.MINOR.PATCH-PRERELEASE+BUILDMETADATA```

**MAJOR**, MINOR, and PATCH versions are rigid in their requirements.  Please refer to the [SemVer2 documentation](https://semver.org) for details. PRERELEASE and BUILD are optional and leave a fair bit of room for organizations to implement what they want. The only caveat is that build metadata cannot be used to differentiate two different packages. Build metadata should not be used when determining version precedence. There are two primary questions:
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
- Build metadata typically includes sha, which makes identification of the source of bits easy.

**CONS:**
- Some related infrastructure systems may not support the ingestion of multiple assets with the same name/version.  For example, MyGet, NuGet, or VSTS support overwriting to varying degrees. You're left to deal with these scenarios on a case-by-case basis.  For example:
  - Introduce a new meaningless commit to bump the version number
  - Successive builds of the same sha, if they were to have different outputs (e.g. a checked vs. debug build) may require a clean of the cache.
  - Temporarily unlock a feed to enable re-publishing (may not be possible, and bad practice)
  - Add logic into all processes to gracefully understand and handle overwrite cases (e.g. are bits the same? then okay)
  - Telemetry systems must understand reruns of the same build.
- Most of the .NET Core builds aren't bit-for-bit identical at the same commit given the same inputs.  If they were, then a partial-respin can gracefully handle dealing with overwrites.
- Date agnostic version doesn't encode inputs (e.g. checked build vs. release build).  This can make it tricky to run 'non-standard' build scenarios if they interact with external systems.
- Generating the same package version in multiple builds with different outputs bits means that package caches must be cleared.

### Date-Varying Versioning
**PROS:**
- Respins are easier.  No overwriting except for stable versions at the end of the product cycle.  Stable versions at the end of the cycle typically avoid external overwrite-averse systems.
- Non-standard builds (e.g. different input parameters) do not collide with standard builds at the same sha when dealing with external systems.
- File versions must be ever increasing to correctly layout new files for servicing (MSI requirement)
- Does not affect the determinism of the build. Input dates are just another build parameter (e.g. OfficialBuildId) and rerunning a build with the same input date should produce equivalent binaries.

**CONS:**
- Requires orchestration to produce a coherent set of versions across multiple build legs
- More difficult to reproduce version identical bits (need to know input parameters)
- Source sha not easily identifiable based on asset version.

### Conclusion

Date agnostic versioning is more hassle than it's worth, though having the sha in the output version number is also useful.  We should combine a sha in the build metadata with the build date+revision (short data + number of builds so far today) to generate a date-varying, unique, identifiable build.

## Build Determinism

  There is often concern around build determinism when date-varying versioning is used. It is important to note that date-varying versioning does not affect the abiliy to have deterministic builds in either the local dev or official build lab scenarios.  The date is either a provided parameter or obtained from git information, meaning that setting it to a specific value at a specific commit can enable the production of the same outputs over and over again.  Date varying versioning only says that **by default** this input varies from build to build.

## Version Fields

- **MAJOR** - Major version
- **MINOR** - Minor version
- **PATCH** - Patch version
- **PRERELEASE** - Prerelease label
- **REVISION** - Number of official builds during the current day
- **SHORTDATE** - 5 digit date
- **SHORTSHA** - Shortened sha of current commit

**Note that version fields should not be zero-padded**

## Versioning States and Scenarios

Versioning comes in 3 states, depending on the point in the product cycle:
- **Dev/Daily** - Versions should include all fields - pre-release tag, shortdate, revision, etc.
  ```
  MAJOR.MINOR.PATCH-PRERELEASE.SHORTDATE.REVISION+SHORTSHA
  ```
  Example:
  ```
    1.0.0-preview1.25405.01+abcdef
  ```
- **Final Prerelease** - Versions should include **MAJOR**, **MINOR**, **PATCH**, and **PRERELEASE** tag but no **SHORTDATE**, **REVISION**, or **SHORTSHA**.  **PRERELEASE** should be suffixed with `'.final'`.  This avoids a common issue in nuget package resolution where `2.1.0-rc1` < `2.1.0-rc1.12345`. The intention is that the final build is resolved over the date-versioned build.
  ```
  MAJOR.MINOR.PATCH-PRERELEASE.final
  ```
  Example:
  ```
    1.0.0-preview1.final
  ```
- **Stable** - Versions should include **MAJOR**, **MINOR**, **PATCH**
  ```
  MAJOR.MINOR.PATCH
  ```
  Example:
  ```
    1.0.0
  ```
## Version Field Generation
- **MAJOR** - Explicit in source, should default to `1`
- **MINOR** - Explicit in source, should default to `0`
- **PATCH** - Explicit in source, should default to `0`
- **PRERELEASE** - Explicit in source, should default to `preview1`
- **REVISION** - Generated based on build scenario
  - **Local dev builds** - Defaulted to 0
  - **Official builds** - Supplied generally as part of the conventional, .NET Core specific `OfficialBuildId` or VSTS `Build.BuildNumber` built in parameters.
- **SHORTDATE** - Generated based on build scenario
  - **Local dev builds** - Date of current git HEAD
  - **Official builds** - Supplied generally as part of the conventional, .NET Core specific `OfficialBuildId` or VSTS `Build.BuildNumber` built in parameters.
  
  The short date is generated based off the seed date:
  ```
  generateShortDate(seedDate) {
    if (comparisonDate == "") {
      comparisonDate = 1996/04/01 (UTC)
    }
    if (seeDate < comparisonDate) { error }
    months = (seedDate.Year-comparisonDate.Year)*12 + (seedDate.Month - comparisonDate.Month)
    days = seedDate.Day
    return (3 digits padded of months) + (2 digits padded of days)
  }
  ```
- **SHORTSHA** - Parsed from the current git HEAD

## SemVer1 Fallback

In cases where SemVer2 cannot be used (e.g. old versions of nuget), we can fall back to [SemVer1](https://semver.org/spec/v1.0.0.html).  In Semver1, there is no built in build metadata, and the prerelease field may only contain [0-9A-Za-z-].  To comply, cases where + or . are used in SemVer2's prerelease field are replaced with -.

Versioning comes in 3 states, depending on the point in the product cycle:
- **Dev/Daily** - Versions should include all fields - pre-release tag, shortdate, revision, etc.  **For SemVer1, the short date should be zero padded.**
  ```
  MAJOR.MINOR.PATCH-PRERELEASE-SHORTDATE-REVISION-SHORTSHA
  ```
  Example:
  ```
    1.0.0-preview1-25405-01-abcdef
  ```
- **Final Prerelease** - Versions should include **MAJOR**, **MINOR**, **PATCH**, and **PRERELEASE** tag but no **SHORTDATE**, **REVISION**, or **SHORTSHA**.  **PRERELEASE** should be suffixed with `'-final'`.  This avoids a common issue in nuget package resolution where `2.1.0-rc1` < `2.1.0-rc1-12345`. The intention is that the final build is resolved over the date-versioned build.
  ```
  MAJOR.MINOR.PATCH-PRERELEASE-final
  ```
  Example:
  ```
    1.0.0-preview1-final
  ```
- **Stable** - Versions should include **MAJOR**, **MINOR**, **PATCH**
  ```
  MAJOR.MINOR.PATCH
  ```
  Example:
  ```
    1.0.0
  ```