Microsoft.DotNet.Build.Tasks.NuGet
==================================

Contains tasks related to file NuGet.

See ["Task Packages"](../../Documentation/TaskPackages.md#usage) for guidance on installing this package.

Tasks in this package

 - PackNuspc

## Tasks

This package contains the following MSBuild tasks.

### `PackNuspec`

Creates a NuGet package from a .nuspec file.

Task parameter           | Type        | Description
-------------------------|-------------|--------------------------------------------------------------------------------
FilePath                 | string      | **[Required]** The path to the .nuspec file to pack
OutputPath               | string      | The full file path to where nupkg file will be placed.
OutputDirectory          | string      | The directory where nupkg file will be placed. Default file name is "$(packageId).$(version).nupkg"
Overwrite                | boolean     | Overwrite files if they exists already in DestinationFolder or OutputPath. Defaults to false.
Version                  | string      | The package version to use. Overrides any value in .nuspec or `Properties`.
Properties               | string[]    | Provides substitution in the nuspec for variables using `$varName$` syntax. Input should be "key=value" pairs.
BaseDirectory            | string      | The base path to use for any relative paths in the &lt;files&gt; section of nuspec.
Dependencies             | ITaskItem[] | Dependencies to add to the &lt;dependencies&gt; section of the spec. <br> Metadata 'TargetFramework' can be specified to put dependencies into groups with targetFramework set.<br> These dependencies augment any dependencies listed explicitly in the .nuspec file.
PackageFiles             | ITaskItem[] | Files to add to the package. Must specify the PackagePath metadata. <br> These files augment any files listed explicitly in the .nuspec file.
IncludeEmptyDirectories  | boolean     | Pack empty directories.
Packages                 | ITaskItem[] | **[Output]** The full path to package files created.


Notes:
 - Either OutputPath or DestinationFolder must be specified.

Example:
```xml
<PackNuspec FilePath="MyPackage.nuspec" 
            Version="$(PackageVersion)"
            Properties="author=$(Author);$description=$(Description)"
            OutputDirectory="$(PackagesDir)" />
```

```xml
<!-- MyPackage.nuspec -->
<?xml version=`1.0` encoding=`utf-8`?>
<package xmlns=`http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd`>
  <metadata>
    <id>MyPackage</id>
    <version>1.0.0</version>
    <authors>$author$</authors>
    <description>$description$</description>
  </metadata>
  <files />
</package>
```
