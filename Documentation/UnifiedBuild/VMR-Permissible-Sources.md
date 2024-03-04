# The Unified Build Almanac (TUBA) - Permissible Source Policy

## Purpose

This document provides guidelines on the types of source code that are permissible in the VMR. It explains how the VMR detects binaries and licenses, and it outlines the steps to be taken when a new binary or license is encountered.

## Binaries

### Policy

OSS licensed binaries are allowed in the VMR, but they require explicit inclusion in the VMR.

This action involves adding the binary or its exclusion pattern to either [`allowed-binaries.txt`](https://github.com/dotnet/dotnet/blob/main/src/installer/src/VirtualMonoRepo/allowed-binaries.txt) or [`disallowed-sb-binaries.txt`](https://github.com/dotnet/dotnet/blob/main/src/installer/src/VirtualMonoRepo/disallowed-sb-binaries.txt). Which file to add the binary or exclusion pattern to depends on whether or not the binary is allowed for source build. For help determining if the newly detected binary is allowed in the source-build context, please contact a member of the source-build team at @dotnet/source-build-internal.

If the binary is allowed for source build, add it to `allowed-binaries.txt`. This binary will not be removed during a source build of the product and it will no longer be considered a new binary in the VMR.

If the binary is not allowed for source build, add it to `disallowed-binaries.txt`. This binary will be removed during a source build of the product, but it will not longer be considered a new binary in the VMR.

When adding a binary or pattern to either file, remember to include a link to the relevant issue and tag @dotnet/source-build-internal as a reviewer.

### Detection and Removal

The [BinaryTool](https://github.com/dotnet/dotnet/tree/main/eng/tools/BinaryToolKit) is used to detect and remove binaries in the VMR. You can run the tool locally by running `eng/prep-source-build.sh`.

For detection, the tool identifies binaries not listed in `allowed-binaries.txt` and `disallowed-sb-binaries.txt`. Any binary not listed in these files is considered "new" and requires explicit action as discussed above.

For removal, the tool identifies binaries not listed in `allowed-binaries.txt`. Any binary not listed in this file is not allowed for source-building and will be removed from the VMR.

## Licenses

### Policy

The VMR does not permit code and binaries licensed under non-OSS licenses.

When a non-OSS license is detected, the offending code and binaries must be cloaked from the VMR. See [the VMR](./VMR-Design-And-Operation.md#repository-source-mappings) and [source-build documentation](https://github.com/dotnet/source-build/blob/main/Documentation/sourcebuild-in-repos/new-repo.md#cloaking-filtering-the-repository-sources) on cloaking.

### Detection

Licenses are detected by the [license scan smoke test](https://github.com/dotnet/dotnet/blob/main/test/Microsoft.DotNet.SourceBuild.SmokeTests/LicenseScanTests.cs). This smoke test is run as part of the [source-build license scan pipeline](https://dev.azure.com/dnceng/internal/_build?definitionId=1301&_a=summary). The test detects any license in the VMR that is not listed in [`LicenseExclusions.txt`](https://github.com/dotnet/dotnet/blob/main/test/Microsoft.DotNet.SourceBuild.SmokeTests/assets/LicenseExclusions.txt).