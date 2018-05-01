# Microsoft.DotNet.Build.Tasks.Versioning
===============================

Task package which handles generation of version strings. 
The format that we use is described here: Documentation/Versioning.md

## Sample Usage

Property        | Type        | Description
----------------|-------------|--------------------------------------------------------------------------------
Major           | string      | Major version number.
Minor           | string      | Minor version number.
Patch           | string      | Patch number.
Prerelease      | string      | Prerelease label. E.g. "beta", "preview", etc.
ShortDate       | string      | Date to be used in the version string.
Builds          | string      | Number of builds for current date.
ShortSha        | string      | SHA of the repo last commit.

FormatName      | string      | The name of the format string you want to use. Defaults are "dev", "stable" and "final".
FormatStrings   | ItemGroup   | Your custom format strings.

VersioningError | boolean     | **Output** Indicate whether an error happened.
VersionString   | string      | **Output** Version string produced.

```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">

  <PropertyGroup>
    <Major>1</Major>
    <Minor>2</Minor>
    <Patch>3</Patch>
    <Prerelease>beta1</Prerelease>
    <ShortDate>3111985</ShortDate>
    <Builds>5</Builds>
    <ShortSha>asd23fer</ShortSha>
    
    <SomethingCustom>Custom format:</SomethingCustom>

    <FormatName>final</FormatName>
  </PropertyGroup>
  
  <ItemGroup>
    <FormatStrings Include="dev">
      <Format>$(SomethingCustom) $(major).$(minor).$(patch)-$(prerelease).$(shortdate).$(builds)+$(shortsha)</Format>
    </FormatStrings>

    <FormatStrings Include="stable">
      <Format>$(SomethingCustom) $(major).$(minor).$(patch)-$(prerelease)</Format>
    </FormatStrings>

    <FormatStrings Include="final">
      <Format>$(SomethingCustom) $(major).$(minor).$(patch)</Format>
    </FormatStrings>
  </ItemGroup>
  
  <Target Name="TestIt" DependsOnTargets="Versioning">
    <Message Text="Version string is: $(VersionString)" Condition="'$(VersioningError)'=='false'" />
    <Message Text="Error creating version string." Condition="'$(VersioningError)'=='true'" />
  </Target>

  <Import Condition="Exists('Versioning.props')" Project="Versioning.props" />

</Project>
```
