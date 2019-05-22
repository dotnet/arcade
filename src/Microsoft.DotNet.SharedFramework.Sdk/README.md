# Microsoft.DotNet.SharedFramework.Sdk

Projects which need to create a .NET Core shared framework can use this SDK to generate some of the required content. Shared frameworks
generally require at least two kinds of packages, so the recommended usage is to create two projects, one for the targeting pack and another for the runtime packs. 
Both projects can use a custom MSBuild SDK, "Microsoft.DotNet.SharedFramework.Sdk" and set some well-known properties which will inform the
custom SDK how to build a shared framework.

### Project file API

Example usage for the runtime project

```xml
<Project Sdk="Microsoft.DotNet.SharedFramework.Sdk">
  <PropertyGroup>
     <!-- Required properties -->
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SharedFrameworkName>Microsoft.Banana.App</SharedFrameworkName>
    <PlatformPackageType>RuntimePack</PlatformPackageType>
    
    <!--  This will be the default, but be overridden by the project file. -->
    <PackageId>Microsoft.Banana.App.Runtime.$(RuntimeIdentifier)</PackageId>
  </PropertyGroup>
  
  <ItemGroup>
    <FrameworkReference Include="Microsoft.NETCore.App" RuntimeFrameworkVersion="3.0.0" />
    
    <ProjectReference Include="..\..\MyAssembly1\MyAssembly1.csproj" />
    <ProjectReference Include="..\..\MyAssembly2\MyAssembly2.csproj" />
    
    <NativeRuntimeAsset Include="linux-x64/libmydep.so" Condition=" '$(RuntimeIdentifier)' == 'linux-x64' " />
  </ItemGroup>
  
</Project>
```

Example usage for the targeting pack project


```xml
<Project Sdk="Microsoft.DotNet.SharedFramework.Sdk">
  <PropertyGroup>
     <!-- Required properties -->
    <TargetFramework>netcoreapp3.0</TargetFramework>
    <SharedFrameworkName>Microsoft.Banana.App</SharedFrameworkName>
    <PlatformPackageType>TargetingPack</PlatformPackageType>
    
    <!--  This will be the default, but be overridden by the project file. -->
    <PackageId>Microsoft.Banana.App.Ref</PackageId>
  </PropertyGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\..\MyAssembly1\MyAssembly1.csproj" />
    <ProjectReference Include="..\..\MyAssembly2\MyAssembly2.csproj" />
    
    <!--  Optimization for assemblies that are provided in both a targeting pack and an OOB NuGet package.  -->
    <ProvidesPackage Include="MyAssembly1" Version="1.0.0" />
  </ItemGroup>
</Project>
```

## Properties

### `SharedFrameworkName`

This is the user-facing name of the framework. Traditionally ends in '.App'. Examples: Microsoft.NETCore.App, Microsoft.AspNetCore.App

### `PlatformPackageType`

This determines which kind of manifests and assets are included in the package. One of two values:
* `TargetingPack` - generate a .NET Core targeting pack
* `RuntimePack` - generate a .NET Core runtime pack

### `RuntimeIdentifier`

When PlatformPackageType=RuntimePack, this property must be set. It is used to determine with runtime identifier the current project targets.

### `TargetFramework`

Determines which version of .NET Core this project is compatible with.
