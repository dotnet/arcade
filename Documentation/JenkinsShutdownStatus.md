# Jenkins Shutdown Status

## Overview

Jenkins for CI has been replaced by Azure DevOps.  **The last day for Jenkins support of .NET Core 3.0 branches is May 3rd, 2019.**  It is expected that all .NET Core 3.0 repos / branches will be using Azure DevOps for CI by May 3rd and that Jenkins is no longer used for CI.

This document is tracking the status of .NET Core 3.0 repos using Jenkins for CI and provides guidance for how to disable Jenkins for a repo / branch.

## Steps for disabling Jenkins

1. Remove entry from [dotnet-ci](https://github.com/dotnet/dotnet-ci/blob/master/data/repolist.txt)
2. Delete repos netci.groovy file

The Jenkins generator job and associated jobs *should* delete themselves. However, if it doesn’t you can delete the full folder for the repo/branch combo on the server:

1. Log into Jenkins instance
2. Find repo/branch folder
3. Hit Delete Folder on left
4. Confirm.

## .NET Core 3.0 Status Overview

| Repo                       | Owner            | Status      | Risk   | Curent Jenkins jobs | Notes |
| ---------------------------| ---------------- |:-----------:|:------:| ------------------- | ----- |
| Arcade                     | mawilkie         | -           | -      | - ||
| aspnet-AspNetCore          | namc             | -           | -      | - ||
| aspnet-AspNetCore-Tooling  | namc             | -           | -      | - ||
| aspnet-EntityFrameworkCore | namc             | -           | -      | - ||
| aspnet-Extensions          | namc             | -           | -      | - ||
| CLI                        | licavalc         | -           | -      | - ||
| CLICommandLineParser       | licavalc         | -           | -      | - ||
| CLI-Migrate                | licavalc         | -           | -      | - ||
| **CoreClr**                | russellk         | In progress | Medium | ci1, ci2, ci3 | Under discussion: the highest risk is getting the CoreFx jobs for CoreClr working in Azure DevOps. There are a lot of other jobs that need to be ported but they are low risk. (CoreClr really wants queue time parameters to be supported via comment triggers and that work is scheduled for Q2) |
| CoreFx                     | danmose          | -           |        | - ||
| Core-SDK                   | licavalc         | -           | -      | - ||
| Core-Setup                 | dleeapho         | -           | -      | - ||
| **dotnet-docker**          | msimons          | In progress | Low    | [ci1](https://ci.dot.net/job/dotnet_dotnet-docker/) | [Tracking issue](https://github.com/dotnet/dotnet-docker/issues/744), no progress expected until after 4/9 (patch Tuesday) |
| **dotnet-framework-docker**| msimons          | In progress | Low    | [ci1](https://ci.dot.net/job/Microsoft_dotnet-framework-docker/) | [Tracking issue](https://github.com/Microsoft/dotnet-framework-docker/issues/225), no progress expected until after 4/9 (patch Tuesday) |
| docker-tools               | msimons          | -           |        | - ||
|**dotnet-buildtools-prereqs-docker** | msimons | In progress | Low    | [ci1](https://ci.dot.net/job/dotnet_dotnet-buildtools-prereqs-docker/) | [Tracking issue](https://github.com/dotnet/dotnet-buildtools-prereqs-docker/issues/84), no progress expected until after 4/9 (patch Tuesday) |
| MSBuild                    | licavalc         | -           | -      | - ||
| Roslyn                     | jaredpar         | -           | -      | - ||
| **SDK**                    | licavalc         | In progress | Low    | [ci2](https://ci2.dot.net/job/dotnet_sdk/) | Planned, moving perf job to Azure DevOps |
| Standard                   | danmose          | -           | -      | - ||
| SymReader                  | tmat             | -           | -      | - ||
| SymReader-Portable         | tmat             | -           | -      | - ||
| Templating                 | vramak           | -           | -      | - ||
| **TestFx**                 | sarabjot         | In progress | Low    | [ci1](https://ci.dot.net/job/Microsoft_testfx/job/master/) | Planned |
| Test-Templates             | sasin            | -           | -      | - ||
| Toolset                    | licavalc         | -           | -      | - ||
| VisualFSharp               | brettfo          | -           | -      | - ||
| VSTest                     | sarabjot         | -           | -      | - ||
| WebSDK                     | vramak           | -           | -      | - ||
| WinForms                   | mmcgaw           | -           | -      | - ||
| WPF                        | vatsan-madhavan  | -           | -      | - ||

`-` means an item is complete or not needed

## Additional Jenkins jobs

Additionally, here are other Jenkins jobs which are not specifically part of the core product repos, but are likely candidates for deletion or moving to Azure DevOps.  It would be great if we could get owners attached to these jenkins jobs and a plan for them being disabled from Jenkins.

| Job name                      | Owner        | release | VS | Perf | Other | Notes |
| ----------------------------- | ------------ | --------| -- | ---- | ----- | ----- |
| aspnet_aspnet-docker          | dougbu       | -       | -  | -    | [dev](https://ci.dot.net/job/aspnet_aspnet-docker/job/dev/) [master](https://ci.dot.net/job/aspnet_aspnet-docker/job/master/) | Needed until at least June |
| aspnet-EntityFrameworkCore    | dougbu       | [2.1](https://ci.dot.net/job/aspnet_EntityFrameworkCore/job/release_2.1/) [2.2](https://ci.dot.net/job/aspnet_EntityFrameworkCore/job/release_2.2/) | - | - | - |       |
| dotnet_CITest                 | mmitche      | -       | -  | -    | [stability](https://ci2.dot.net/job/dotnet_citest/job/stability/) ||
| dotnet-CLI                    | licavalc     | [2.1.5xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.5xx/) [2.1.6xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.6xx/) [2.1.7xx](https://ci.dot.net/job/dotnet_cli/job/release_2.1.7xx/) [2.2.1xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.1xx/) [2.2.2xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.2xx/) [2.2.3xx](https://ci.dot.net/job/dotnet_cli/job/release_2.2.3xx/) [1.0.0](https://ci.dot.net/job/dotnet_cli/job/rel_1.0.0/) [1.0.1](https://ci.dot.net/job/dotnet_cli/job/rel_1.0.1/) [1.1.0](https://ci.dot.net/job/dotnet_cli/job/rel_1.1.0/) | - | - | - ||
| dotnet_Core                   | mmitche      | -       | -  | -    | [master](https://ci2.dot.net/job/dotnet_core/job/master/) ||
| dotnet-CoreClr                | russellk     | [2.1](https://ci.dot.net/job/dotnet_coreclr/job/release_2.1/) [2.2](https://ci.dot.net/job/dotnet_coreclr/job/release_2.2/) [1.0.0](https://ci.dot.net/job/dotnet_coreclr/job/release_1.0.0/) [1.1.0](https://ci.dot.net/job/dotnet_coreclr/job/release_1.1.0/) | - |[perf](https://ci2.dot.net/job/dotnet_coreclr/job/perf/) | [pipelines](https://ci.dot.net/job/dotnet_coreclr/job/pipelines/) [uwp6.2](https://ci.dot.net/job/dotnet_coreclr/job/release_uwp6.2/) ||
| dotnet-CoreFx                 | danmose      | [2.1](https://ci.dot.net/job/dotnet_corefx/job/release_2.1/) [2.2](https://ci.dot.net/job/dotnet_corefx/job/release_2.2/) [1.0.0](https://ci.dot.net/job/dotnet_corefx/job/release_1.0.0/) [1.1.0](https://ci.dot.net/job/dotnet_corefx/job/release_1.1.0/) | - | [perf](https://ci2.dot.net/job/dotnet_corefx/job/perf/) | [pipelines](https://ci.dot.net/job/dotnet_corefx/job/pipelines/) [uwp6.2](https://ci.dot.net/job/dotnet_corefx/job/release_uwp6.2/) ||
| dotnet_CoreRt                 | sergeyk      | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_corert/job/master/) ||
| dotnet-Core-Setup             | dleeapho     | [2.1](https://ci.dot.net/job/dotnet_core-setup/job/release_2.1/) [2.2](https://ci.dot.net/job/dotnet_core-setup/job/release_2.2/) [1.0.0](https://ci.dot.net/job/dotnet_core-setup/job/release_1.0.0/) [1.1.0](https://ci.dot.net/job/dotnet_core-setup/job/release_1.1.0/) | - | - | [pipelines](https://ci.dot.net/job/dotnet_core-setup/job/pipelines/) [uwp6.0](https://ci.dot.net/job/dotnet_core-setup/job/release_uwp6.0/) ||
| dotnet_Interactive-Window     | tmat         | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_interactive-window/job/master/) | Already moved to Azure DevOps and can be removed? |
| dotnet_Metadata-Tools         | tmat         | -       | -  | -    | [master](https://ci2.dot.net/job/dotnet_metadata-tools/job/master/) | Already moved to Azure DevOps and can be removed? |
| dotnet_Orleans                | sbykov       | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_orleans/job/master/) ||
| dotnet_Perf-Infra             | anscoggi     | -       | -  | -    | [stability](https://ci2.dot.net/job/dotnet_perf-infra/job/stability/) [startup](https://ci2.dot.net/job/dotnet_perf-infra/job/startup/) ||
| dotnet_Performance            | michelm      | -       | -  | [perf](https://ci2.dot.net/job/dotnet_performance/job/perf/) | -     ||
| dotnet_Roslyn-Analyzers       | tmat         | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/master/) [2.6.x](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/2.6.x/) [2.9.x](https://ci.dot.net/job/dotnet_roslyn-analyzers/job/2.9.x/)| Planned |
| dotnet_Roslyn-Tools           | tmat         | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_roslyn-tools/job/master/) ||
| dotnet_SDK                    | licavalc     | [2.1.5xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.5xx/) [2.1.6xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.6xx/) [2.1.7xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.1.7xx/) [2.2.1xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.1xx/) [2.2.2xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.2xx/) [2.2.3xx](https://ci.dot.net/job/dotnet_sdk/job/release_2.2.3xx/) [1.0.0](https://ci.dot.net/job/dotnet_sdk/job/rel_1.0.0/) [1.1.0](https://ci.dot.net/job/dotnet_sdk/job/rel_1.1.0/) | -       | [perf](https://ci2.dot.net/job/dotnet_sdk/job/perf/) | [experimental-classic-projects](https://ci.dot.net/job/dotnet_sdk/job/experimental-classic-projects/) ||
| dotnet_Standard               | danmose      | [2.0.0](https://ci.dot.net/job/dotnet_standard/job/release_2.0.0/) | -  | -    | - ||
| dotnet_SymReader              | tmat         | -       | -  | -    | [1.3.0](https://ci.dot.net/job/dotnet_symreader/job/release_1.3.0/) ||
| dotnet_SymReader-Converter    | tmat         | -       | -  | -    | [master](https://ci2.dot.net/job/dotnet_symreader-converter/job/master/) | Planned |
| dotnet_SymReader-Portable     | tmat         | -       | -  | -    | [1.5.0](https://ci.dot.net/job/dotnet_symreader-portable/job/release_1.5.0/) ||
| dotnet_Templating             | vramak       | [2.1](https://ci.dot.net/job/dotnet_templating/job/release_2.1/) | - | - | [stabilize](https://ci.dot.net/job/dotnet_templating/job/stabilize/) ||
| dotnet_Versions               | mmitche      | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_versions/job/master/) ||
| dotnet_WCF                    | stebon       | [2.1](https://ci.dot.net/job/dotnet_wcf/job/release_2.1.0/) [2.0.0](https://ci.dot.net/job/dotnet_wcf/job/release_2.0.0/) [1.0.0](https://ci.dot.net/job/dotnet_wcf/job/release_1.0.0/)[1.1.0](https://ci.dot.net/job/dotnet_wcf/job/release_1.1.0/) | - | - | [master](https://ci.dot.net/job/dotnet_wcf/job/master/)  [uwp6.0](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.0/) [uwp6.1](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.1/) [uwp6.2](https://ci.dot.net/job/dotnet_wcf/job/release_uwp6.2/) ||
| dotnet_Xliff-Tasks            | tomescht     | -       | -  | -    | [master](https://ci.dot.net/job/dotnet_xliff-tasks/job/master/) ||
| drewscoggins_corefx           | drewscoggins | -       | -  | [perf](https://ci2.dot.net/job/drewscoggins_corefx/job/perf/) |||
| Microsoft_ChakraCore          | louisl       | external | external | external | external ||
| Microsoft_ConcordExtensibilityS greggm       | external | external | external | external ||
| Microsoft_MIEngine            | waan         | external | external | external | external ||
| Microsoft_MSBuild             | licavalc     | -       | [vs15.5](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.5/) [vs15.6](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.6/) [vs15.7](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7/) [vs15.7-preview4](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7-preview4/) [vs15.7_sourcebuild](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.7_sourcebuild/) [vs15.8](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.8/) [vs15.9](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.9/) [vs15.9stg](https://ci2.dot.net/job/Microsoft_msbuild/job/vs15.9stg/) | - | - ||
| Microsoft_PartsUnlimited      | davete       | external | external | external | external ||
| Microsoft_TestFx              | sarabjot     | [1.2.1](https://ci.dot.net/job/Microsoft_testfx/job/1.2.1/) | - | - | - ||
| Microsoft_Vipr                | mmainer      | external | external | external | external ||
| Microsoft_VisualFSharp        | brettfo      | -       | [vs15.5](https://ci2.dot.net/job/Microsoft_visualfsharp/job/dev15.5/) [vs15.7](https://ci2.dot.net/job/Microsoft_visualfsharp/job/dev15.7/) [vs15.8](https://ci2.dot.net/job/Microsoft_visualfsharp/job/dev15.8/) | - | - ||
| Microsoft_XUnitPerformance    | jorive       | -       | - | - | [master](https://ci.dot.net/job/Microsoft_xunit-performance/job/master/) [citest](https://ci.dot.net/job/Microsoft_xunit-performance/job/citest/) ||
| mono_linker                   | svbomer      | -       | - | - | [master](https://ci.dot.net/job/mono_linker/job/master/) ||
| pxt*                          | peli de halleux | external | external | external | external ||

