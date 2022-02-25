# IBCMerge during the CoreFX build

This is an implementation plan to enable IBC training data merging during the CoreFX official build.

The CoreFX official build fetches IBCMerge.exe using the internal tooling flow. See [fetch-internal-tooling.md](fetch-internal-tooling.md).

Packages containing IBC data are restored using a project with an auto-updated dependency. Tentatively in https://github.com/dotnet/corefx/tree/master/external.

If IBC merging is enabled by an msbuild property, the build uses `ibcmerge.exe` to merge IBC data into assemblies where applicable. A BuildTools target performs the merging between the `build` and `sign` sections of the build. The result is that signed official binaries contain merged IBC info.
