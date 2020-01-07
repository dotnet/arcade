# Microsoft.DotNet.Build.Tasks.SharedFramework.Sdk

Common toolset for building shared frameworks and framework packs. Handles
projects with extensions:

* `depproj`
  * Restores dependencies and run crossgen.
* `pkgproj`
  * Generates NuGet packages and various installers: Windows `msi`, macOS `pkg`,
    Debian packages, RPM packages, and the `tar.gz` and `zip` archives.
* `sfxproj`
  * Generates the shared framework ("runtime") itself, its installers, and its
    compressed archives.
* `bundleproj`
  * Generates bundle installers: Windows `exe`.
  * There is a macOS bundle `pkg`, however it is not produced by this package as
    of writing.

Framework packs are targeting packs, apphost packs, and runtime packs. See the
design at https://github.com/dotnet/designs/pull/50.

This package is a migration of tooling previously living in the
[Core-Setup](https://github.com/dotnet/core-setup) repository, without any
significant improvements.
[arcade#2704](https://github.com/dotnet/arcade/issues/2704) tracks improving it
to the point where it's reasonable to onboard new repos.

This package depends on `Microsoft.DotNet.Build.Tasks.Packaging` for NuGet
package creation and validation tooling.
