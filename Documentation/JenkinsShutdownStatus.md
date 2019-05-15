# Jenkins Shutdown Status

## Overview

This document is tracking the status of repos using Jenkins for CI and provides guidance for how to disable Jenkins for a repo / branch.

## Steps for disabling Jenkins

1. Remove entry from [dotnet-ci](https://github.com/dotnet/dotnet-ci/blob/master/data/repolist.txt)
2. Delete repos netci.groovy file

The Jenkins generator job and associated jobs *should* delete themselves. However, if it doesn’t you can delete the full folder for the repo/branch combo on the server:

1. Log into Jenkins instance
2. Find repo/branch folder
3. Hit Delete Folder on left
4. Confirm.

## Jenkins - .NET Core 3.0

Jenkins for CI has been replaced by Azure DevOps.  **The last day for Jenkins support of .NET Core 3.0 branches is May 3rd, 2019.**  It is expected that all .NET Core 3.0 repos / branches will be using Azure DevOps for CI by May 3rd and that Jenkins is no longer used for CI.

| Repo                       | Owner            | Status      | Risk   | Curent Jenkins jobs | Notes |
| ---------------------------| ---------------- |:-----------:|:------:| ------------------- | ----- |
| **CoreClr**                | russellk         | Complete* || [ci1](https://ci.dot.net/job/dotnet_coreclr/) | *CoreCLR will continue to leverage Jenkins for certain automated outerloop testing as long as it is around, but these are not blocking Jenkins shutdown.|
| **dotnet-docker**          | msimons          | Complete    ||||
| **dotnet-framework-docker**| msimons          | Complete    ||||
|**dotnet-buildtools-prereqs-docker** | msimons | Complete    ||||
| **SDK**                    | licavalc         | Complete    ||||
| **TestFx**                 | sarabjot         | Complete    ||||

`-` means an item is complete or not needed

## Jenkins - Release

[.NET Core release support dates](https://github.com/dotnet/core/blob/master/microsoft-support.md#net-core-releases)
- EOL for 1.x is June 27, 2019

- EOL for 2.1.x is at least three years from LTS declaration (August 21, 2018)


| Job name                      | Owner        | Status | Jenkins jobs | Notes |
| ----------------------------- | ------------ | ------ | ------------ | ----- |
| aspnet-EntityFrameworkCore    | dougbu       | Complete | [2.1](https://ci.dot.net/job/aspnet_EntityFrameworkCore/job/release_2.1/), [2.2](https://ci.dot.net/job/aspnet_EntityFrameworkCore/job/release_2.2/) | Brice Lamson to confirm if these are needed |
| dotnet-CLI                    | licavalc     | In Progress | [2.1.5xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.5xx/), [2.1.6xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.6xx/), [2.1.7xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.7xx/), [2.2.1xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.1xx/), [2.2.2xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.2xx/), [2.2.3xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.3xx/), [1.0.0](https://ci.dot.net/job/dotnet_cli/job/rel_1.0.0/), [1.0.1](https://ci.dot.net/job/dotnet_cli/job/rel_1.0.1/), [1.1.0](https://ci.dot.net/job/dotnet_cli/job/rel_1.1.0/) ||
| dotnet-CoreClr                | russellk     || [2.1](https://ci.dot.net/job/dotnet_coreclr/job/release_2.1/), [2.2](https://ci.dot.net/job/dotnet_coreclr/job/release_2.2/), [1.0.0](https://ci.dot.net/job/dotnet_coreclr/job/release_1.0.0/), [1.1.0](https://ci.dot.net/job/dotnet_coreclr/job/release_1.1.0/), [2.1/2.2 perf](https://ci2.dot.net/job/dotnet_coreclr/job/perf/) ||
| dotnet-CoreFx                 | danmose      | In Progress | [2.1](https://ci.dot.net/job/dotnet_corefx/job/release_2.1/), [2.2](https://ci.dot.net/job/dotnet_corefx/job/release_2.2/), [1.0.0](https://ci.dot.net/job/dotnet_corefx/job/release_1.0.0/), [1.1.0](https://ci.dot.net/job/dotnet_corefx/job/release_1.1.0/), [2.1/2.2 perf](https://ci2.dot.net/job/dotnet_corefx/job/perf/)||
| dotnet-Core-Setup             | dleeapho     | Complete | [2.1](https://ci.dot.net/job/dotnet_core-setup/job/release_2.1/), [2.2](https://ci.dot.net/job/dotnet_core-setup/job/release_2.2/), [1.0.0](https://ci.dot.net/job/dotnet_core-setup/job/release_1.0.0/), [1.1.0](https://ci.dot.net/job/dotnet_core-setup/job/release_1.1.0/) ||
| dotnet_SDK                    | licavalc     | In Progress | [2.1.5xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.5xx/), [2.1.6xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.6xx/), [2.1.7xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.7xx/), [2.2.1xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.1xx/), [2.2.2xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.2xx/), [2.2.3xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.3xx/), [1.0.0](https://ci.dot.net/job/dotnet_sdk/job/rel_1.0.0/), [1.1.0](https://ci.dot.net/job/dotnet_sdk/job/rel_1.1.0/) ||
| dotnet_Standard               | danmose      | Complete | [2.0.0](https://ci.dot.net/job/dotnet_standard/job/release_2.0.0/) ||
| dotnet_Templating             | vramak       | Complete | [2.1](https://ci.dot.net/job/dotnet_templating/job/release_2.1/) | Jobs were |
| dotnet_WCF                    | stebon       | stebon discussing a plan with alicia li | [2.1](https://ci.dot.net/job/dotnet_wcf/job/release_2.1.0/), [2.0.0](https://ci.dot.net/job/dotnet_wcf/job/release_2.0.0/), [1.0.0](https://ci.dot.net/job/dotnet_wcf/job/release_1.0.0/), [1.1.0](https://ci.dot.net/job/dotnet_wcf/job/release_1.1.0/) ||

## Jenkins - VS

[Product lifecycle](https://docs.microsoft.com/en-us/visualstudio/releases/2019/servicing)

| Job name                      | Owner        | Status | Jenkins jobs | Notes |
| ----------------------------- | ------------ | ------ | ------------ | ----- |
| Microsoft_MSBuild             | licavalc     || [vs15.9](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.9/) ||
| Microsoft_VisualFSharp        | brettfo      | Complete |||

## Jenkins - UWP

| Job name                      | Owner        | Status | Jenkins jobs | Notes |
| ----------------------------- | ------------ | ------ | ------------ | ----- |
| dotnet-CoreClr                | russellk     || [uwp6.2](https://ci.dot.net/job/dotnet_coreclr/job/release_uwp6.2/) ||
| dotnet-CoreFx                 | danmose      || [uwp6.2](https://ci.dot.net/job/dotnet_corefx/job/release_uwp6.2/) ||
| dotnet-Core-Setup             | dleeapho     || [uwp6.0](https://ci.dot.net/job/dotnet_core-setup/job/release_uwp6.0/) ||
| dotnet_WCF                    | stebon       | stebon discussing a plan with alicia li | [uwp6.0](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.0/), [uwp6.1](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.1/), [uwp6.2](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.2/) ||

## Jenkins - Other

| Job name                      | Owner        | Status | Jenkins jobs | Notes |
| ----------------------------- | ------------ | ------ | ------------ | ----- |
| aspnet_aspnet-docker          | dougbu       || [dev](https://ci.dot.net/job/aspnet_aspnet-docker/job/dev/), [master](https://ci.dot.net/job/aspnet_aspnet-docker/job/master/) | Needed until at least June |
| dotnet_CITest                 | mmitche      | Complete | [stability](https://ci2.dot.net/job/dotnet_citest/job/stability/) | Not Needed |
| dotnet_Core                   | mmitche      | Complete | [master](https://ci2.dot.net/job/dotnet_core/job/master/) ||
| dotnet-CoreClr                | russellk     || [pipelines](https://ci.dot.net/job/dotnet_coreclr/job/pipelines/) ||
| dotnet-CoreFx                 | danmose      | Complete | [pipelines](https://ci.dot.net/job/dotnet_corefx/job/pipelines/)||
| dotnet_CoreRt                 | sergeyk      | Waiting for WASM queues which will be available with the next rollout | [master](https://ci.dot.net/job/dotnet_corert/job/master/) ||
| dotnet-Core-Setup             | dleeapho     | Complete | [pipelines](https://ci.dot.net/job/dotnet_core-setup/job/pipelines/) ||
| dotnet_Interactive-Window     | tmat         | Complete | [master](https://ci.dot.net/job/dotnet_interactive-window/job/master/) | Already moved to Azure DevOps and can be removed? |
| dotnet_Metadata-Tools         | tmat         | Complete | [master](https://ci2.dot.net/job/dotnet_metadata-tools/job/master/) | Already moved to Azure DevOps and can be removed? |
| dotnet_Orleans                | sbykov       || [master](https://ci.dot.net/job/dotnet_orleans/job/master/) ||
| dotnet_Perf-Infra             | anscoggi     | Complete | [stability](https://ci2.dot.net/job/dotnet_perf-infra/job/stability/), [startup](https://ci2.dot.net/job/dotnet_perf-infra/job/startup/) ||
| dotnet_Roslyn-Analyzers       | tmat         | Complete | [master](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/master/), [2.6.x](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/2.6.x/), [2.9.x](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/2.9.x/)| Planned |
| dotnet_Roslyn-Tools           | tmat         | Complete | [master](https://ci.dot.net/job/dotnet_roslyn-tools/job/master/) ||
| dotnet_SDK                    | licavalc     | Complete | [experimental-classic-projects](https://ci.dot.net/job/dotnet_sdk/job/experimental-classic-projects/) | Removed, old branch not needed. |
| dotnet_SymReader              | tmat         | Complete | [1.3.0](https://ci.dot.net/job/dotnet_symreader/job/release_1.3.0/) | Removed, not needed |
| dotnet_SymReader-Converter    | tmat         || [master](https://ci2.dot.net/job/dotnet_symreader-converter/job/master/) | Planned |
| dotnet_SymReader-Portable     | tmat         | Complete | [1.5.0](https://ci.dot.net/job/dotnet_symreader-portable/job/release_1.5.0/) | Removed, not needed |
| dotnet_Templating             | vramak       | Complete | [stabilize](https://ci.dot.net/job/dotnet_templating/job/stabilize/) | No actual jenkins jobs |
| dotnet_Versions               | mmitche      | Complete | [master](https://ci.dot.net/job/dotnet_versions/job/master/) ||
| dotnet_WCF                    | stebon       | stebon discussing a plan with alicia li | [master](https://ci.dot.net/job/dotnet_wcf/job/master/) ||
| dotnet_Xliff-Tasks            | tomescht     | Complete | [master](https://ci.dot.net/job/dotnet_xliff-tasks/job/master/) ||
| Microsoft_ChakraCore          | louisl       || external | jrprather is managing migration |
| Microsoft_ConcordExtensibilitySamples | greggm || external | jrprather is managing migration |
| Microsoft_MIEngine            | waan         || external | Done |
| Microsoft_PartsUnlimited      | davete       || external | jrprather is managing migration |
| Microsoft_TestFx              | sarabjot     | Complete | [1.2.1](https://ci.dot.net/job/Microsoft_testfx/job/1.2.1/) ||
| Microsoft_Vipr                | mmainer      || external | jrprather is managing migration |
| Microsoft_XUnitPerformance    | jorive       || [master](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/), [citest](https://ci.dot.net/job/Microsoft_xunit-performance/job/citest/) ||
| mono_linker                   | svbomer      | Complete | [master](https://ci.dot.net/job/mono_linker/job/master/) ||
| pxt*                          | peli de halleux || external | jrprather is managing migration |

