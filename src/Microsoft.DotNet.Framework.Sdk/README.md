# Microsoft.DotNet.Framework.Sdk

Projects which need to create .NET platform packages can use this SDK to
generate some of the required content.

.NET Core runtimes generally require at least two kinds of packages, so the
recommended usage is to create two projects, one for the targeting pack and
another for the runtime packs. Both projects can use a custom MSBuild SDK,
"Microsoft.DotNet.Framework.Sdk" and set some well-known properties which will
inform the custom SDK how to build a shared framework.

### Project file API

Example usage for a project that produces a .NET Core runtime pack.

```xml
<Project Sdk="Microsoft.DotNet.Framework.Sdk">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <FrameworkName>Microsoft.Banana.App</FrameworkName>
    <FrameworkPackageType>RuntimePack</FrameworkPackageType>
    <RuntimeIdentifiers>win-x64;win-x86;osx-x64;linux-x64;linux-arm64</RuntimeIdentifiers>
    <!--
      Note: when building, RuntimeIdentifier (singular) is required.
      This can be set by using dotnet build -r $(rid) if building the project
      directly, or by setting <RuntimeIdentifier>$(FromSomeOtherProperty)</RuntimeIdentifier>.
    -->
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.NETCore.App" RuntimeFrameworkVersion="3.0.0" />

    <ProjectReference Include="..\..\MyAssembly1\MyAssembly1.csproj" />
    <ProjectReference Include="..\..\MyAssembly2\MyAssembly2.csproj" />
  </ItemGroup>

</Project>
```

Example usage for a targeting pack project

```xml
<Project Sdk="Microsoft.DotNet.Framework.Sdk">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <FrameworkName>Microsoft.Banana.App</FrameworkName>
    <FrameworkPackageType>TargetingPack</FrameworkPackageType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\MyAssembly1\MyAssembly1.csproj" />
    <ProjectReference Include="..\..\MyAssembly2\MyAssembly2.csproj" />
  </ItemGroup>
</Project>
```

## Properties

### `FrameworkName`

This is the user-facing name of the framework.

Examples:
* Microsoft.NETCore.App
* Microsoft.AspNetCore.App
* NETStandard.Library

### `FrameworkPackageType`

This determines which kind of manifests and assets are included in the package.
The SDK currently supports two values:

* `TargetingPack` - generate a .NET targeting pack
* `RuntimePack` - generate a .NET Core runtime pack

### `RuntimeIdentifier`

When FrameworkPackageType=RuntimePack, this property must be set. It is used to
determine with runtime identifier the current project targets.

### `TargetFramework`

Determines which version of .NET Core this project is compatible with.

