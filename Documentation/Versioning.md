# .NET Core Ecosystem v2 - Versioning

.NET Core repositories should use [SemVer2](https://semver.org) for their asset versioning scheme. Versions take the general form of:

```MAJOR.MINOR.PATCH-PRERELEASE+BUILDMETADATA```

MAJOR, MINOR, and PATCH versions are rigid in their requirements.  Please refer to the [SemVer2 documentation](https://semver.org) for details. PRERELEASE and BUILD are optional and leave a fair bit of room for organizations to implement what they want. The only caveat is that build metadata cannot be used to differentiate two different packages. Build metadata should not be used when determining version precedence. There are two primary questions:
- What goes in the PRERELEASE and BUILD fields and when are they used?
- Should we use stable versioning vs. date varying versioning?

## Stable vs. Date-Varying Build Versioning

In the context of the .NET Core product, a stable build version number is stable if successive builds produce produce the same build version numbers at the same input sha. Historically, .NET Core has had repositories that implement stable versioning, via build numbers that increment based on commit depth, as well as repositories that base their build number off of the date and number of builds prior on the day (date based versioning)

Both types of versioning have advantages and disadvantages:
### Stable Versioning
**PROS:**
- Assets with identical version metadata can be reproduced without outside input (e.g. providing a build id)
- Parallel, independent legs of the same build do not require parent orchestration and will generate coherent versioning.  Separate parts of the build can potentially be respun as required.
- We already have 'stable' (but build metadata free) versioning at the end of a product cycle.
- Build metadata typically includes sha, which makes identification of the source of bits easy.

**CONS:**
- Some related infrastructure systems may not support the ingestion of multiple assets with the same name/version.  For example, MyGet, NuGet, or VSTS support overwriting to varying degrees. You're left to deal with these scenarios on a case-by-case basis.  For example:
  - Introduce a new meaningless commit to bump the version number
  - Successive builds of the same sha, if they were to have different outputs (e.g. a checked vs. debug build) may require a clean of the cache.
  - Temporarily unlock a feed to enable re-publishing (may not be possible, and bad practice)
  - Add logic into all processes to gracefully understand and handle overwrite cases (e.g. are bits the same? then okay)
  - Telemetry systems must understand reruns of the same build.
- Most of the .NET Core builds aren't bit-for-bit identical at the same commit given the same inputs.  If they were, then a partial-respin can gracefully handle dealing with overwrites.
- Stable version doesn't encode inputs (e.g. checked build vs. release build).  This can make it tricky to run 'non-standard' build scenarios if they interact with external systems.

### Date-Varying Versioning
**PROS:**
- Respins are easier.  No overwriting except for stable versions at the end of the product cycle.  Stable versions at the end of the cycle typically avoid external overwrite-averse systems.
- Non-standard builds (e.g. different input parameters) do not collide with standard builds at the same sha when dealing with external systems.
- File versions must be ever increasing to correctly layout new files for servicing (MSI requirement)

**CONS:**
- Requires orchestration to produce a coherent set of versions across multiple build legs
- More difficult to reproduce version identical bits (need to know input parameters)
- Source sha not easily identifiable based on asset version.

### Conclusion

Stable version is more hassle than it's worth, though having the sha in the output package version is also useful.  We should combine a sha in the build metadata with the build date+revision (short data + number of builds so far today) to generate a non-stable, unique, identifiable build.

## Versioning Details

We will use the following form:

```
MAJOR.MINOR.PATCH-PRERELEASE.SHORTDATE.BUILDS+SHORTSHA
```

Where:
- **MAJOR** - Major version
- **MINOR** - Minor version
- **PATCH** - Patch version
- **PRERELEASE** - alphanumeric prerelease version like `preview1`, `preview3` or `beta2`
- **SHORTDATE** - shortened date (5 digits)
- **BUILDS** - Number of builds already started today, starting at 0.  No leading 0s
- **SHORTSHA** - shortened sha

## Examples

- 1.2.0-preview1.08530.0+asdf34234
- 1.2.0-preview1 (stabilized)
- 3.0.1-beta2.26405.10+asd34523
- 3.0.1 (stabilized)

## Version Number Generation
MAJOR, MINOR, PRERELEASE and PATCH version fields are explicit in the source.  The rest of the fields are generated or supplied:

**todo - add generation info**