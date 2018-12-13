# Arcade SDK

Arcade SDK is a set of msbuild props and targets files and packages that provide common build features used across multiple repos, such as CI integration, packaging, VSIX and VS setup authoring, testing, and signing via Microbuild.

The infrastructure of each [repository that contributes to .NET Core 3.0 stack](TierOneRepos.md) is built on top of Arcade SDK. This allows us to orchestrate the build of the entire stack as well as build the stack from source. These repositories are expected to be on the latest version of the Arcade SDK.

Repositories that do not participate in .NET Core 3.0 build may also use Arcade SDK in order to take advantage of the common infrastructure.

The goals are

- to reduce the number of copies of the same or similar props, targets and script files across repos
- enable cross-platform build that relies on a standalone dotnet cli (downloaded during restore) as well as desktop msbuild based build
- be as close to the latest shipping .NET Core SDK as possible, with minimal overrides and tweaks
- be modular and flexible, not all repos need all features; let the repo choose subset of features to import
- unify common operations and structure across repos
- unify Azure DevOps Build Pipelines used to produce official builds

The toolset has four kinds of features and helpers:

- Common conventions applicable to all repos using the toolset.
- Infrastructure required for Azure DevOps CI builds, MicroBuild and build from source.
- Workarounds for bugs in shipping tools (dotnet SDK, VS SDK, msbuild, VS, NuGet client, etc.).
  Will be removed once the bugs are fixed in the product and the toolset moves to the new version of the tool.
- Abstraction of peculiarities of VSSDK and VS insertion process that are not compatible with dotnet SDK.

The toolset has following requirements on the repository layout.

## Single build output

All build outputs are located under a single directory called `artifacts`. 
The Arcade SDK defines the following output structure:

```text
artifacts
  bin
    $(MSBuildProjectName)
      $(Configuration)
  packages
    $(Configuration)
      $(MSBuildProjectName).$(PackageVersion).nupkg
  TestResults
    $(Configuration)
      $(MSBuildProjectName)_$(TargetFramework)_$(TestArchitecture).(xml|html|log|error.log)
  VSSetup
    $(Configuration)
      Insertion
        $(VsixPackageId).json
        $(VsixPackageId).vsmand
        $(VsixContainerName).vsix
        $(VisualStudioInsertionComponent).vsman
      $(VsixPackageId).json
      $(VsixContainerName).vsix
  VSSetup.obj
    $(Configuration)
      $(VisualStudioInsertionComponent)
  SymStore
    $(Configuration)
      $(MSBuildProjectName)
  log
    $(Configuration)
      Build.binlog
  tmp
    $(Configuration)
  obj
    $(MSBuildProjectName)
      $(Configuration)
  toolset
```

Having a common output directory structure makes it possible to unify MicroBuild definitions.

| directory         | description |
|-------------------|-------------|
| bin               | Build output of each project. |
| obj               | Intermediate directory for each project. |
| packages          | NuGet packages produced by all projects in the repo. |
| VSSetup           | Packages produced by VSIX projects in the repo. These packages are experimental and can be used for dogfooding.
| VSSetup/Insertion | Willow manifests and VSIXes to be inserted into VS.
| VSSetup.obj       | Temp files produced by VSIX build. |
| SymStore          | Storage for converted Windows PDBs |
| log               | Build binary log and other logs. |
| tmp               | Temp files generated during build. |
| toolset           | Files generated during toolset restore. |

## Build scripts and extensibility points

### Build scripts

Some common build scripts for using Arcade are provided in the [eng/common](https://github.com/dotnet/arcade/tree/master/eng/common) folder.  The general entry points are `cmd` or `sh` files and they provide common arguments to the `build.ps1` or `build.sh` files.

Common build scripts include:

- [CIBuild.cmd](https://github.com/dotnet/arcade/blob/master/eng/common/CIBuild.cmd) | [cibuild.sh](https://github.com/dotnet/arcade/blob/master/eng/common/cibuild.sh) - build script for running an Arcade build in CI or official builds

Most repos additionally create repo specific build scripts which reference the `eng/common/build.ps1` or `eng/common/build.sh` files.  They provide default arguments to those scripts but pass along any additional arguments you specify.

Repo script examples include:

- [Build.cmd](https://github.com/dotnet/arcade/blob/master/Build.cmd) | [build.sh](https://github.com/dotnet/arcade/blob/master/build.sh) - default wrapper script for building and restoring the repo.
- [Restore.cmd](https://github.com/dotnet/arcade/blob/master/Restore.cmd) | [restore.sh](https://github.com/dotnet/arcade/blob/master/restore.sh) - default wrapper script for restoring the repo.
- [Test.cmd](https://github.com/dotnet/arcade/blob/master/Test.cmd) | [test.sh](https://github.com/dotnet/arcade/blob/master/test.sh) - default wrapper script for running tests in the repo.

Since the default scripts pass along additional arguments, you could restore, build, and test a repo by running `Build.cmd -test`.

You should feel free to create more repo specific scripts as appropriate to meet common dev scenarios for your repo.

### /eng/common/*

The Arcade SDK requires bootstrapper scripts to be present in the repo.

The scripts in this directory shall be present and the same across all repositories using Arcade SDK.

### /eng/Build.props

Provide repo specific Build properties such as the list of projects to build.

#### Arcade project building

By default, Arcade [builds solutions in the root of the repo](https://github.com/natemcmaster/arcade/blob/5b83384fa2b2534491314794f6da2338f90ffbc7/src/Microsoft.DotNet.Arcade.Sdk/tools/Build.proj#L62-L64).  Overriding the default build behavior may be done by either of these methods.

- Provide the project list on the command-line.

  Example: `build.cmd -projects MyProject.proj`

  See [source code](https://github.com/dotnet/arcade/blob/440b2dae3a206b28f6aba727b7818873358fcc0a/eng/common/build.ps1#L53)

- Provide a repo default override in `eng/Build.props`.

  Example:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <ItemGroup>
      <ProjectToBuild Include="$(MSBuildThisFileDirectory)..\MyProject.proj" />
    </ItemGroup>
  </Project>
  ```

  CoreFx does not use the default build projects in its repo - [example](https://github.com/dotnet/corefx/blob/66392f577c7852092f668876822b6385bcafbd44/eng/Build.props)

#### Example: specifying a solution to build

This is often required for repos which have multiple .sln files in the root directory.

```xml
<!-- eng/Build.props -->
<Project>
  <ItemGroup>
    <ProjectToBuild Include="$(RepoRoot)MySolution1.sln" />
  </ItemGroup>
</Project>
```

#### Example: building project files instead of solutions

You can also specify a list of projects to build instead of building .sln files.

```xml
<!-- eng/Build.props -->
<Project>
  <ItemGroup>
    <ProjectToBuild Include="$(RepoRoot)src\**\*.csproj" />
  </ItemGroup>
</Project>
```

#### Example: conditionally specifying which projects to build

You can use custom MSBuild properties to control the list of projects which build.

```xml
<!-- eng/Build.props -->
<Project>
  <ItemGroup>
    <!-- Usage: build.cmd /p:BuildMyOptionalGroupOfStuff=true -->
    <ProjectToBuild Condition="'$(BuildMyOptionalGroupOfStuff)' == 'true"
                    Include="$(RepoRoot)src\feature1\**\*.csproj" />

    <!-- Only build some projects when building on Windows  -->
    <ProjectToBuild Condition="'$(OS)' == 'Windows_NT"
                    Include="$(RepoRoot)src\**\*.vcxproj" />

    <!-- You can also use MSBuild Include/Exclude syntax -->
    <ProjectToBuild Include="$(RepoRoot)src\**\*.csproj"
                    Exclude="$(RepoRoot)src\samples\**\*.csproj" />
  </ItemGroup>
</Project>
```

### /eng/Versions.props: A single file listing component versions and used tools

The file is present in the repo and defines versions of all dependencies used in the repository, the NuGet feeds they should be restored from and the version of the components produced by the repo build.

```xml
<Project>
  <PropertyGroup>
    <!-- Base three-part version used for all outputs of the repo (assemblies, packages, vsixes) -->
    <VersionPrefix>1.0.0</VersionPrefix>
    <!-- Package pre-release suffix not including build number -->
    <PreReleaseVersionLabel>rc2</PreReleaseVersionLabel>
  
    <!-- Opt-in repo features -->
    <UsingToolVSSDK>true</UsingToolVSSDK>
    <UsingToolIbcOptimization>true</UsingToolIbcOptimization>

    <!-- Opt-out repo features -->
    <UsingToolXliff>false</UsingToolXliff>
  
    <!-- Versions of other dependencies -->   
    <MyPackageVersion>1.2.3-beta</MyPackageVersion>
  </PropertyGroup>
  
  <PropertyGroup>
    <!-- Feeds to use to restore dependent packages from. -->  
    <RestoreSources>
      $(RestoreSources);
      https://dotnet.myget.org/F/myfeed/api/v3/index.json
    </RestoreSources>
  </PropertyGroup>
</Project>
```

The toolset defines a set of tools (or features) that each repo can opt into or opt out. Since different repos have different needs the set of tools that will be imported from the toolset can be controlled by `UsingTool{tool-name}` properties, where *tool-name* is e.g. `Xliff`, `SourceLink`, `XUnit`, `VSSDK`, `IbcOptimization`, etc. These properties shall be set in the Versions.props file. 

The toolset also defines default versions for various tools and dependencies, such as MicroBuild, XUnit, VSSDK, etc. These defaults can be overridden in the Versions.props file.

See [DefaultVersions](https://github.com/dotnet/arcade/blob/master/src/Microsoft.DotNet.Arcade.Sdk/tools/DefaultVersions.props) for a list of *UsingTool* properties and default versions.

### /eng/Tools.props (optional)

Specify package references to additional tools that are needed for the build.
These tools are only used for build operations performed outside of the repository solution (such as additional packaging, signing, publishing, etc.).

### /eng/AfterSolutionBuild.targets (optional)

Targets executed in a step right after the solution is built.

### /eng/AfterSigning.targets (optional)

Targets executed in a step right after artifacts has been signed.

### /global.json, /nuget.config: SDK configuration

`/global.json` file is present and specifies the version of the dotnet and `Microsoft.DotNet.Arcade.Sdk` SDKs.

For example,

```json
{
  "tools": {
    "dotnet": "2.1.400-preview-009088"
  },
  "msbuild-sdks": {
    "Microsoft.DotNet.Arcade.Sdk": "1.0.0-prerelease-63208-02"
  }
}
```

Include `vs` entry under `tools` if the repository should be built via `msbuild` from Visual Studio installation instead of dotnet cli:

```json
{
  "tools": {
    "vs": {
      "version": "15.9"
    }
  }
}
```

Optionally, a list of Visual Studio [workload component ids](https://docs.microsoft.com/en-us/visualstudio/install/workload-component-id-vs-enterprise) may be specified under `vs`:

```json
"vs": {
  "version": "15.9",
  "components": ["Microsoft.VisualStudio.Component.VSSDK"]
}
```

`/NuGet.config` file is present and specifies the MyGet feed to retrieve Arcade SDK from like so:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <!-- Only specify feed for Arcade SDK (see https://github.com/Microsoft/msbuild/issues/2982) -->
  <packageSources>
    <clear />
    <add key="dotnet-core" value="https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json" />
  </packageSources>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
</configuration>
```

> An improvement in SKD resolver is proposed to be able to specify the feed in `global.json` file to avoid the need for extra configuration in `NuGet.config`. See https://github.com/Microsoft/msbuild/issues/2982.

### /src/Directory.Build.props

`Directory.Build.props` shall import Arcade SDK as shown below. The `Sdk.props` file sets various properties and item groups to default values. It is recommended to perform any customizations _after_ importing the SDK.

It is a common practice to specify properties applicable to all (most) projects in the repository in `Directory.Build.props`, e.g. public keys for `InternalsVisibleTo` project items.

```xml
<PropertyGroup>  
  <PropertyGroup>
    <ImportNetSdkFromRepoToolset>false</ImportNetSdkFromRepoToolset>
  </PropertyGroup>

  <Import Project="Sdk.props" Sdk="Microsoft.DotNet.Arcade.Sdk" />    

  <!-- Public keys used by InternalsVisibleTo project items -->
  <MoqPublicKey>00240000048000009400...</MoqPublicKey> 
</PropertyGroup>
```

### Directory.Build.targets

`Directory.Build.targets` shall import Arcade SDK. It may specify additional targets applicable to all source projects.

```xml
<Project>
  <Import Project="Sdk.targets" Sdk="Microsoft.DotNet.Arcade.Sdk" />
</Project>
```

### Source Projects

Projects are located under `src` directory under root repo, in any subdirectory structure appropriate for the repo. 

Projects shall use `Microsoft.NET.Sdk` SDK like so:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    ...
</Project>
```

### Project name conventions

- Unit test project file names shall end with `.UnitTests` or `.Tests`, e.g. `MyProject.UnitTests.csproj` or `MyProject.Tests.csproj`. 
- Integration test project file names shall end with `.IntegrationTests`, e.g. `MyProject.IntegrationTests.vbproj`.
- Performance test project file names shall end with `.PerformanceTests`, e.g. `MyProject.PerformaceTests.csproj`.
- If `source.extension.vsixmanifest` is present next to the project file the project is by default considered to be a VSIX producing project.

## Other Projects

It might be useful to create other top-level directories containing projects that are not standard C#/VB/F# projects. For example, projects that aggregate outputs of multiple projects into a single NuGet package or Willow component. These projects should also be included in the main solution so that the build driver includes them in build process, but their `Directory.Build.*` may be different from source projects. Hence the different root directory.

## Building VSIX packages (optional)

Building Visual Studio components is an opt-in feature of the Arcade SDK. Property `UsingToolVSSDK` needs to be set to `true` in the `Versions.props` file.

Set `VSSDKTargetPlatformRegRootSuffix` property to specify the root suffix of the VS hive to deploy to.

If `source.extension.vsixmanifest` is present next to a project file the project is by default considered to be a VSIX producing project. 
A package reference to `Microsoft.VSSDK.BuildTools` is automatically added to such project. 
A project that needs `Microsoft.VSSDK.BuildTools` for generating pkgdef file needs to include the PackageReference explicitly.

Arcade SDK include build target for generating VS Template VSIXes. Adding `VSTemplate` items to project will trigger the target.

`source.extension.vsixmanifest` shall specify `Experimental="true"` attribute in `Installation` section. The experimental flag will be stripped from VSIXes inserted into Visual Studio.

VSIX packages are built to `VSSetup` directory.

## Visual Studio Insertion components (optional)

To include the output VSIX of a project in Visual Studio Insertion, set the `VisualStudioInsertionComponent` property.
Multiple VSIXes can specify the same component name, in which case their manifests will be merged into a single insertion unit.

The Visual Studio insertion manifests and VSIXes are generated during Pack task into `VSSetup\Insertion` directory, where they are picked by by MicroBuild Azure DevOps publishing task during official builds.

Arcade SDK also enables building VS Setup Components from .swr files (as opposed to components comprised of one or more VSIXes).

Use `SwrProperty` and `SwrFile` items to define a property that will be substituted in .swr files for given value and the set of .swr files, respectively.

For example,

```xml
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFramework>net46</TargetFramework>
    <VisualStudioInsertionComponent>Microsoft.VisualStudio.ProjectSystem.Managed</VisualStudioInsertionComponent>
  </PropertyGroup>
  <ItemGroup>
    <SwrProperty Include="Version=$(VsixVersion)" />
    <SwrProperty Include="VisualStudioXamlRulesDir=$(VisualStudioXamlRulesDir)" />
  </ItemGroup>
  <ItemGroup>
    <SwrFile Include="*.swr" />
  </ItemGroup>
</Project>
```

Where .swr file is:

```text
use vs

package name=Microsoft.VisualStudio.ProjectSystem.Managed.CommonFiles
        version=$(Version)

vs.localizedResources
  vs.localizedResource language=en-us
                       title="Microsoft VisualStudio Managed Project System Common Files"
                       description="Microsoft VisualStudio ProjectSystem for C#/VB/F#(Managed) Projects"

folder "InstallDir:MSBuild\Microsoft\VisualStudio\Managed"
  file source="$(VisualStudioXamlRulesDir)Microsoft.CSharp.DesignTime.targets"
  file source="$(VisualStudioXamlRulesDir)Microsoft.VisualBasic.DesignTime.targets"
  file source="$(VisualStudioXamlRulesDir)Microsoft.FSharp.DesignTime.targets"
  file source="$(VisualStudioXamlRulesDir)Microsoft.Managed.DesignTime.targets"
```

## MicroBuild

The repository shall define a YAML Pipeline that uses MicroBuild (e.g. `azure-pipelines.yml`).

The following step shall be included in the definition:

```yml
- task: ms-vseng.MicroBuildTasks.30666190-6959-11e5-9f96-f56098202fef.MicroBuildSigningPlugin@1
  displayName: Install Signing Plugin
  inputs:
    signType: real
    esrpSigning: true
  condition: and(succeeded(), ne(variables['SignType'], ''))
```

```yml
- script: eng\common\CIBuild.cmd 
          -configuration $(BuildConfiguration)
          /p:OfficialBuildId=$(BUILD.BUILDNUMBER)
          /p:DotNetSignType=$(SignType)
          /p:DotNetSymbolServerTokenMsdl=$(microsoft-symbol-server-pat)
          /p:DotNetSymbolServerTokenSymWeb=$(symweb-symbol-server-pat)
  displayName: Build
```

```yml
- task: PublishTestResults@1
  displayName: Publish Test Results
  inputs:
    testRunner: XUnit
    testResultsFiles: 'artifacts/$(BuildConfiguration)/TestResults/*.xml'
    mergeTestResults: true
    testRunTitle: 'Unit Tests'
  condition: succeededOrFailed()
```

The above build steps assume the following variables to be defined:

- `SignType`, which specified the kind signing type: "real" (default) or "test"

The Build Pipeline also needs to link the following variable group:

- DotNet-Symbol-Publish 
  - `microsoft-symbol-server-pat`
  - `symweb-symbol-server-pat`

## Project Properties Defined by the SDK

### `SemanticVersioningV1` (bool)

`true` if `Version` needs to respect SemVer 1.0. Default is `false`, which means format following SemVer 2.0.

### `IsShipping` (bool)

`true` if the package (NuGet or VSIX) produced by the project is _shipping_. 

Set `IsShipping` property to `false`

- projects that produce NuGet packages that are not shipping on NuGet.org or via other official channel (like part of an official installer), 
- projects that produce VSIX packages that are only used only within the repository (e.g. to facilitate integration tests or VS F5) and not expected to be installed by customers,
- Test/build utility projects (test projects are automatically marked as non-shipping).

All packages are VSIXes are signed by default, regardless of whether they are _shipping_ or not.
By default Portable and Embedded PDBs produced by _shipping_ projects are converted to Windows PDBs and published to Microsoft symbol servers.

### `PublishWindowsPdb` (bool)

`true` (default) if the PDBs produced by the project should be converted to Windows PDB and published to Microsoft symbol servers.
Set to `false` to override the default (uncommon).

### `ApplyPartialNgenOptimization` (bool)

Set to `true` in a shipping project to require IBC optimization data to be available for the project and embed them into the binary during official build. 

### `SkipTests` (bool)

Set to `true` in a test project to skip running tests.

### `TestArchitectures` (list of strings)

List of test architectures (`x64`, `x86`) to run tests on.
If not specified by the project defaults to the value of `PlatformTarget` property, or `x64` if `Platform` is `AnyCPU` or unspecified.

For example, a project that targets `AnyCPU` can opt-into running tests using both 32-bit and 64-bit test runners on .NET Framework by setting `TestArchitectures` to `x64;x86`.

### `TestTargetFrameworks` (list of strings)

By default, the test runner will run tests for all frameworks a test project targets. Use `TestTargetFrameworks` to reduce the set of frameworks to run against.

For example, consider a project that has `<TargetFrameworks>netcoreapp2.1;net472</TargetFrameworks>`. To only run .NET Core tests run 

```text
msbuild Project.UnitTests.csproj /p:TestTargetFrameworks=netcoreapp2.1
```

### `TestRuntime` (string)

Runtime to use for running tests. Currently supported values are: `Core` (.NET Core), `Full` (.NET Framework) and `Mono` (Mono runtime).

For example, the following runs .NET Framework tests using Mono runtime:

```text
msbuild Project.UnitTests.csproj /p:TestTargetFrameworks=net472 /p:TestRuntime=Mono
```
