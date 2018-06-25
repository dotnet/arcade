# Microsoft.DotNet.Arcade.Sdk
MSBuild Sdk package for use with arcade-consuming projects

## Usage
Update the following files in your repository

### Directory.Build.props
```xml
<Project>
  <PropertyGroup>
    <RepositoryRoot>$(MSBuildThisFileDirectory)</RepositoryRoot>
  </PropertyGroup>
  <Import Project="Sdk.props" Sdk="Microsoft.DotNet.Arcade.Sdk"/>

  <!-- ... Other Content ... -->
</Project>
```

### Directory.Build.targets
```xml
<Project>
  <!-- ... Other Content ... -->

  <Import Project="Sdk.targets" Sdk="Microsoft.DotNet.Arcade.Sdk"/>
</Project>
```
