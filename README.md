# Arcade

## Overview

Arcade is intended to provide well-understood and consistent mechanisms for consuming, updating, and sharing infrastructure across the .NET Core team. For more details about Arcade, please see the [Overview](./Documentation/Overview.md) documentation.

## Build & Test Status

Azure DevOps [![Build Status](https://dev.azure.com/dnceng/public/_apis/build/status/dotnet/arcade/arcade-ci)](https://dev.azure.com/dnceng/public/_build/latest?definitionId=208)

## Getting Started

Packages are published daily to our tools feed:

> `https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json`

### Source Code

`git clone https://github.com/dotnet/arcade.git`

### How to use Arcade

Arcade tools may be consumed by following the guidelines defined in the [Task Packages](./Documentation/TaskPackages.md) documentation.

You can view the available Arcade produced packages by adding the Arcade [package feed](#getting-started), to your [Visual Studio package source](https://docs.microsoft.com/en-us/nuget/tools/package-manager-ui).

### How to contribute

- How to contribute guide [TBD]

- [Pull requests](https://github.com/dotnet/arcade/pulls): [Open](https://github.com/dotnet/arcade/pulls?q=is%3Aopen+is%3Apr)/[Closed](https://github.com/dotnet/arcade/pulls?q=is%3Apr+is%3Aclosed)

- [Issues](https://github.com/dotnet/arcade/issues)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).  For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

### License

.NET Core (including the Arcade repo) is licensed under the [MIT license](LICENSE.TXT). 
