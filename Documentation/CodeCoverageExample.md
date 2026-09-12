# Code Coverage Example for Arcade SDK

This example demonstrates how to enable code coverage collection in test projects using Arcade SDK.

## Prerequisites

- Arcade SDK 11.0.0 or later
- Test project using XUnit

## Basic Configuration

Add the following properties to your test project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    
    <!-- Enable VSTest runner instead of XUnit console runner -->
    <UseVSTestRunner>true</UseVSTestRunner>
    
    <!-- Enable code coverage collection -->
    <CollectCoverage>true</CollectCoverage>
    
    <!-- Optional: Specify coverage format (default is cobertura) -->
    <CodeCoverageFormat>cobertura</CodeCoverageFormat>
  </PropertyGroup>
</Project>
```

## Running Tests with Coverage

Run tests using the build script:

```bash
./build.sh --test
```

Or using MSBuild directly:

```bash
dotnet build /t:Test /p:Configuration=Release
```

## Coverage Reports

Code coverage reports will be generated in the `artifacts/TestResults/coverage/` directory.

For Cobertura format, the report file will be named `coverage.cobertura.xml` and can be published to Azure DevOps.

## Azure DevOps Integration

Add the following task to your Azure Pipelines YAML to publish coverage results:

```yaml
- task: PublishCodeCoverageResults@2
  inputs:
    summaryFileLocation: '$(Build.SourcesDirectory)/artifacts/TestResults/coverage/**/coverage.cobertura.xml'
    codecoverageTool: 'cobertura'
  displayName: 'Publish Code Coverage Results'
```

## Advanced Configuration

### Filtering Coverage

Exclude specific assemblies or files from coverage:

```xml
<PropertyGroup>
  <!-- Exclude test assemblies and external dependencies -->
  <CoverageExclude>[*.Tests]*;[xunit.*]*;[Moq]*</CoverageExclude>
  
  <!-- Exclude generated files -->
  <CoverageExcludeByFile>**/*Designer.cs;**/Generated/*.cs</CoverageExcludeByFile>
  
  <!-- Exclude obsolete code -->
  <CoverageExcludeByAttribute>Obsolete;GeneratedCode;CompilerGenerated</CoverageExcludeByAttribute>
</PropertyGroup>
```

### Multiple Output Formats

Generate coverage in multiple formats:

```xml
<PropertyGroup>
  <CodeCoverageFormat>cobertura,opencover,lcov</CodeCoverageFormat>
</PropertyGroup>
```

## Troubleshooting

### Coverage not collected

- Ensure `UseVSTestRunner` is set to `true`
- Verify `CollectCoverage` is set to `true`
- Check that `coverlet.collector` package is restored (should be automatic)

### Empty coverage report

- Make sure tests are actually running and passing
- Verify the test project has a reference to the code you want to cover

## See Also

- [Arcade SDK Documentation](../Documentation/ArcadeSdk.md)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
