# Automate Performance testing run in AzDO Pipelines

## Performance scripts

The performance scripts are a powershell and shell script that set up the environment variables necessary for running performance testing. Scripts can be found [here](../eng/common/performance).

## How to add performance testing to a pipeline

The pre-reqs for running performance testing are defined in the template -- [performance.yml](../eng/common/templates/job/performance.yml). The template will need to be added as a job to any repository that wishes to run performance testing. The only required parameter are jobName and pool. If no other parameters are supplied, the testing will pull down the performance repository and run the performance testing against the latest dotnet sdk.

Additional template parameters are:

| Name                 | Type   | Description                               |
| -------------------- | ------ | ----------------------------------------- |
| displayName          | string | The name to display in the Azure Pipeline |
| steps                | array  | Any additional steps that need to be run before running the performance testing (i.e. pulling down the build artifacts, building, etc.) |
| container            | object | The container to run in (if the build machine is linux) |
| extraSetupParameters | string | Extra parameters to send to the performance scripts |
| frameworks           | array  | Dotnet frameworks to run against (i.e. netcoreapp3.0, netcoreapp2.2, etc) |
| continueOnError      | string | Determines whether to continue the build if the step errors |
| dependsOn            | object | Jobs that the performance testing depends on (i.e. Previous build jobs whose artifacts the performance testing job will use) |
| timeoutInMinutes     | int    | How long to wait before timing out |
| enableTelemetry      | bool   | If we should enable telemetry |

## Performance Script Arguments

All parameters that have default values are based off of Azure Pipelines pre-defined variables should only be specified if there is a specific need.

| Name                | Type    | Description                                  |
| ------------------- | ------- | -------------------------------------------- |
| SourceDirectory     | string  | The path to the root of the source directory. Default: $env:BUILD_SOURCESDIRECTORY |
| CoreRootDirectory   | string  | Path to the core root directory. To be used when testing against a built corerun |
| Architecture        | string  | Architecture of the build to be tested (i.e. x64, x86, arm64). Default: x64 |
| Framework           | string  | Dotnet sdk framework to install. To be specified when testing against older frameworks. Default: netcoreapp3.0 |
| CompilationMode     | string  | CompilationMode to use when jitting. Default: Tiered |
| Repository          | string  | Repository that is running perf testing. Default: $env:BUILD_REPOSITORY_NAME |
| Branch              | string  | Branch that is running perf testing. Default: $env:BUILD_SOURCEBRANCH |
| CommitSha           | string  | Sha1 of the current branch. Default: $env:BUILD_SOURCEVERSION |
| BuildNumber         | string  | Build number of the run in Azure Pipeline. Default: $env:BUILD_BUILDNUMBER |
| RunCategories       | string  | Space separated list of test categories to run against. Should match the categories used by the csproj. Default: "coreclr corefx" |
| Csproj              | string  | Relative path from the performance repository root to the csproj to run against. Default: src\benchmarks\micro\MicroBenchmarks.csproj |
| Kind                | string  | Short identifier for the csproj. Should match csproj. Default: micro |
| Internal            | switch  | If this is an official build |
| Configurations      | string  | Space separated list of key=value pairs to describe the build (i.e. "OptimizationLevel=PGO CompilationMode=Tiered"). Default: CompilationMode=$CompilationMode |

Note: These parameters should be passed to the yml template in the extraSetupParameters parameter.

## Usage Examples

### Performance Repository

The performance repository is a special case of perf testing, where we do not need to 1) first pull down the performance repo, and 2) do not run against a corerun, so do not need to supply extra steps.

```yml
# Windows 10 x64 micro benchmarks
- template: /eng/common/templates/job/performance.yml	
  parameters:	
    jobName: windows_10_x64_micro
    displayName: Windows 10 x64 micro
    extraSetupParameters: -Architecture x64
    pool: 
      name: Hosted VS2017
    ${{ if eq(variables['System.TeamProject'], 'public') }}:
      frameworks:
        - netcoreapp3.0  
        - netcoreapp2.2
        - netcoreapp2.1
        - net461

# Ubuntu 1604 x64 ml benchmarks		
- template: /eng/common/templates/job/performance.yml	
  parameters:	
    jobName: ubuntu_1604_x64_ml
    displayName: Ubuntu 1604 x64 mlnet
    pool:
      name: Hosted Ubuntu 1604	
    container: ubuntu_x64_build_container	
    extraSetupParameters: --architecture x64 --csproj src/benchmarks/real-world/Microsoft.ML.Benchmarks/Microsoft.ML.Benchmarks.csproj --kind mlnet --runcategories mldotnet
    frameworks: 
      - netcoreapp3.0	
```

### CoreClr

Performance testing has been fully tested in coreclr. Coreclr, corefx and other repos will need to supply a coreroot directory so that testing is done against new bits. This code will pull down the Windows_NT x64 Release build, performed by a previous job, copy the files to a specified location, build the core_root directory, and then run the steps specified in performance.yml to run performance testing in Helix.

```yml
- template: /eng/common/templates/job/performance.yml
  parameters:
    jobName: perfbuild_windows_x64
    displayName: Windows x64 Performance
    pool: 
        name: NetCorePublic-Pool
        queue: BuildPool.Windows.10.Amd64.VS2017.Open

    # Test job depends on the corresponding build job
    dependsOn: build_Windows_NT_x64_Release
    extraSetupParameters: -CoreRootDirectory $(Build.SourcesDirectory)\bin\tests\Windows_NT.x64.Release\Tests\Core_Root -Architecture x64

    steps:
    # Download product build
    - task: DownloadBuildArtifacts@0
      displayName: Download product build
      inputs:
        buildType: current
        downloadType: single
        artifactName: Windows_NT_x64_Release_build
        downloadPath: $(System.ArtifactsDirectory)

    # Populate Product directory
    - task: CopyFiles@2
      displayName: Populate Product directory
      inputs:
        sourceFolder: $(System.ArtifactsDirectory)/Windows_NT_x64_Release_build
        contents: '**'
        targetFolder: $(Build.SourcesDirectory)/bin/Product/Windows_NT.x64.Release

    # Create Core_Root
    - script: build-test.cmd Release x64 skipmanaged skipnative
      displayName: Create Core_Root
```

## Available parameter values

In general, we expect most repositories to test against the default tests and configurations (Microbenchmarks.csproj, coreclr corefx run categories, x64). However, some users may want to to extend their performance testing by testing on additional benchmark suites, or with additional architectures. Below is listed the available options for various parameters.

| Name                | Options                                  |
| ------------------- | ---------------------------------------- |
| Architecture        | x64, x86 (Windows only), arm64 (linux only) |
| Framework           | netcoreapp3.0, netcoreapp2.2, netcoreapp2.1, net461 (windows only) |
| CompilationMode     | Tiered, NoTiering, FullyJittedNoTiering, MinOpt |
| Csproj              | src\benchmarks\micro\MicroBenchmarks.csproj <br> src\benchmarks\real-world\Microsoft.ML.Benchmarks\Microsoft.ML.Benchmarks.csproj <br> src\benchmarks\real-world\Roslyn\CompilerBenchmarks.csproj |
| RunCategories       | For micro benchmarks: coreclr, corefx <br> For ML benchmarks: mldotnet <br> For Roslyn benchmarks: roslyn|
| Kind                | For microbenchmarks: micro <br> For ML benchmarks: mlnet <br> For Roslyn benchmarks: roslyn |