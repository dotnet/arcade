# Arcade

## Overview

Arcade is intended to provide well-understood and consistent mechanisms for consuming, updating, and sharing infrastructure across the .NET Core team. For more details about Arcade, please see the [Overview](./Documentation/Overview.md) documentation.

## Build & Test Status

VSTS [![Build Status](https://dotnet.visualstudio.com/_apis/public/build/definitions/9ee6d478-d288-47f7-aacc-f6e6d082ae6d/17/badge
)](https://dotnet.visualstudio.com/DotNet-Public/_build/index?definitionId=17&branchName=master)

Jenkins

|    | x64 Debug|x64 Release|
|:--:|:--:|:--:|
|**Windows_NT**|[![Build Status](https://ci.dot.net/job/dotnet_arcade/job/master/job/Windows_NT_Debug/badge/icon)](https://ci.dot.net/job/dotnet_arcade/job/master/job/Windows_NT_Debug/)|[![Build Status](https://ci.dot.net/job/dotnet_arcade/job/master/job/Windows_NT_Release/badge/icon)](https://ci.dot.net/job/dotnet_arcade/job/master/job/Windows_NT_Release/)|
|**Ubuntu 16.04**|[![Build Status](https://ci.dot.net/job/dotnet_arcade/job/master/job/Ubuntu16.04_Debug/badge/icon)](https://ci.dot.net/job/dotnet_arcade/job/master/job/Ubuntu16.04_Debug/)|[![Build Status](https://ci.dot.net/job/dotnet_arcade/job/master/job/Ubuntu16.04_Release/badge/icon)](https://ci.dot.net/job/dotnet_arcade/job/master/job/Ubuntu16.04_Release/)|

## Getting Started

Packages are published daily to our tools feed:

> `https://dotnetfeed.blob.core.windows.net/dotnet-tools-internal/index.json`

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

sdflkjsdlfkjsdfasdfsddsf