# Fetch Internal Tooling

This is an implementation plan for how to fetch sensitive internal tools during a .NET Core build.

## Uploading a new tool

The tool is put in a NuGet package and uploaded to a VSTS feed. VSTS feeds require authentication for any operation, and are secure.

## Fetching during the build

An `internal-tool-runtime/project.json` file in BuildTools specifies all required tooling that is only available from the internal VSTS feed. This is similar to [`tool-runtime/project.json`](https://github.com/dotnet/buildtools/blob/6a1400e631a097587246e973973e9fafe7ab6254/src/Microsoft.DotNet.Build.Tasks/PackageFiles/tool-runtime/project.json).

In the official build, three properties are set for the `sync` call:

```
InternalToolSource=https://devdiv.pkgs.visualstudio.com/_packaging/dotnet-core-internal-tools/nuget/v3/index.json
InternalToolSourceUser=dn-bot
InternalToolSourcePassword=******
```

A target in BuildTools runs before the main project package restore, detects that these properties are set, then restores `internal-tool-runtime/project.json` into the `packages` directory. Build steps that need an internal tool can find it using `PrereleaseResolveNuGetPackageAssets`.

Devs who have the internal tooling packages but don't have convenient access to the VSTS feed can set `InternalToolSource` to a directory to use it as an internal tool package feed.

If `InternalToolSource` isn't set, no internal tooling is restored.

## (Alternative: bootstrap/init-tools) Fetching during tool init

Instead of restoring during the build into the packages directory, internal tools could be restored during the bootstrap/init-tools process. This has benefits:

 * Tools are put in `Tools`: easy to locate during build steps.
 * Uses the same overall process for `internal-tool-runtime` as the established `tool-runtime` flow.
 * Intuitively where tools should be restored.

However, most repos use init-tools, not bootstrap. If CoreFX can't uptake bootstrap yet, the (significant) changes would need to be ported across many repositories' init-tools scripts, or else it would diverge even further.
