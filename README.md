# Arcade

## Overview

Arcade is intended to provide well-understood and consistent mechanisms for consuming, updating, and sharing infrastructure across the .NET Core team. For more details about Arcade, please see the [Overview](./Documentation/Overview.md) documentation.

## Build & Test Status

Status of Arcade public CI builds: [![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/arcade/arcade-ci)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=208)

## Validation & Dependency Flow Status
Status Last Updated: August 10, 2020, 8:45 AM PDT

**[Arcade validation policy and process](Documentation/Validation/Overview.md)**

### Current Version of Arcade in `.NET Eng - Latest`

[Link](https://maestro-prod.westus2.cloudapp.azure.com/2/https:%2F%2Fgithub.com%2Fdotnet%2Farcade/latest/graph) to BARViz

### Latest Version of Arcade Being Validated

[Link](https://maestro-prod.westus2.cloudapp.azure.com/9/https:%2F%2Fdev.azure.com%2Fdnceng%2Finternal%2F_git%2Fdotnet-arcade/latest/graph) to BARViz. 

### Build Statuses

|Repo Name|Current Build Status|
|---|---|
|Runtime|[![Build Status](https://dnceng.visualstudio.com/internal/_apis/build/status/dotnet/runtime/dotnet-runtime-official?branchName=master)](https://dnceng.visualstudio.com/internal/_build/latest?definitionId=679&branchName=master)|
|ASPNETCore|[![Build Status](https://dnceng.visualstudio.com/internal/_apis/build/status/dotnet/aspnetcore/aspnetcore-ci-official?branchName=master)](https://dnceng.visualstudio.com/internal/_build/latest?definitionId=21&branchName=master)|
|Installer|[![Build Status](https://dnceng.visualstudio.com/internal/_apis/build/status/dotnet/installer/DotNet%20Core%20SDK%20(Official)?branchName=master)](https://dnceng.visualstudio.com/internal/_build/latest?definitionId=286&branchName=master)|
|Arcade Official Build|[![Build Status](https://dnceng.visualstudio.com/internal/_apis/build/status/dotnet/arcade/arcade-official-ci?branchName=master)](https://dnceng.visualstudio.com/internal/_build/latest?definitionId=6&branchName=master)| 
|Arcade Validation|[![Build Status](https://dnceng.visualstudio.com/internal/_apis/build/status/dotnet/arcade-validation/dotnet-arcade-validation-official?branchName=master)](https://dnceng.visualstudio.com/internal/_build/latest?definitionId=282&branchName=master)|

### Status of Latest Version of Arcade Being Validated

- Arcade version 5.0.0-beta.20407.3 was promoted to .NET Eng - Latest channel.

## Getting Started

Packages are published daily to our tools feed:

> `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json`

This feed is browsable from here:

> https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-eng

### Source Code

`git clone https://github.com/dotnet/arcade.git`

### How to use Arcade

Documentation, tutorials, and guides may be found in the [Start Here](Documentation/StartHere.md) index. 

### How to contribute

- [How to contribute to Arcade guide](Documentation/Policy/ArcadeContributorGuidance.md)

- [Pull requests](https://github.com/dotnet/arcade/pulls): [Open](https://github.com/dotnet/arcade/pulls?q=is%3Aopen+is%3Apr)/[Closed](https://github.com/dotnet/arcade/pulls?q=is%3Apr+is%3Aclosed)

- [Issues](https://github.com/dotnet/arcade/issues)

### License

.NET Core (including the Arcade repo) is licensed under the [MIT license](LICENSE.TXT). 
