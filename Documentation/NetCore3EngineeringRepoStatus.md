# .NET Core 3 Engineering transition status

## Primary Deliverables / Work Streams

1. Using Azure DevOps for CI
2. Using shared toolset (Arcade SDK)
3. Engineering dependency flow
4. Internal builds from dnceng

Target completion date for these workstreams is 12/14/2018.

## Status Overview

| Repo                                          | Owner                         | Status   | Risk Assessment                                                                           | Completion Status | Plan | Notes |
| --------------------------------------------- | ----------------------------- | -------- |:-----------------------------------------------------------------------------------------:|:-----------------:| ----- |----- |
| [Arcade](#arcade)                             | [mawilkie](#mark-wilkie)      | Complete | ![done](https://findicons.com/files/icons/767/wp_woothemes_ultimate/16/checkmark.png)     | 4 / 4             | | |
| [aspnet-AspNetCore](#aspnetcore)              | [namc](#nate-mcmaster)        | At risk  | ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png)   | 0 / 4             | | Internal builds in dnceng work is in progress - Need plan |
| [aspnet-EntityFrameworkCore](#efcore)         | [namc](#nate-mcmaster)        | At risk  | ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png)   | 0 / 4             | | Internal builds in dnceng work is in progress - Need plan |
| [aspnet-Extensions](#extensions)              | [namc](#nate-mcmaster)        | At risk  | ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png)   | 0 / 4             | | Internal builds in dnceng work is in progress - Need plan |
| [CLI](#cli)                                   | [licavalc](#livar-cunha)      | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png)  | 1 / 4            |[plan](https://github.com/dotnet/cli/blob/master/Documentation/MigrationPlan/CLISDKPlan.md) |  |
| [CLICommandLineParser](#clicommandlineparser) | [licavalc](#livar-cunha)      | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 3 / 4             |[plan](https://github.com/dotnet/cli/blob/master/Documentation/MigrationPlan/CLISDKPlan.md) |  |
| [CLI-Migrate](#climigrate)                    | [licavalc](#livar-cunha)      | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 3 / 4             |[plan](https://github.com/dotnet/cli/blob/master/Documentation/MigrationPlan/CLISDKPlan.md) |  |
| [CoreClr](#coreclr)                           | [russellk](#russ-keldorph)    | At risk  | ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png)| 0 / 4             |[plan](MigrationPlan/CoreClrPlan.md) | Complex repo/build |
| [CoreFx](#corefx)                             | [danmose](#dan-moseley)       | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/CoreFxPlan.md) |  |
| [Core-SDK](#coresdk)                          | [licavalc](#livar-cunha)      | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/cli/blob/master/Documentation/MigrationPlan/CLISDKPlan.md) | Will follow Toolset repo - need plan to go green |
| [Core-Setup](#coresetup)                      | [danmose](#dan-moseley)       | At risk  | ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png)   | 1 / 4             | | Need to establish ownership|
| [MSBuild](#msbuild)                           | [raines](#rainer-sigwald)     | At risk  | ![at risk](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png) | 0 / 4             | | Investigating requirements - need plan to go green |
| [Roslyn](#roslyn)                             | [jaredpar](#jared-parsons)    | On Track  | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png)  | 0 / 4             | [plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/roslyn.md) | Good progress, but need plan to go green |
| [SDK](#sdk)                                   | [licavalc](#livar-cunha)      | Complete | ![done](https://findicons.com/files/icons/767/wp_woothemes_ultimate/16/checkmark.png)     | 4 / 4             | | |
| [Standard](#standard)                         | [danmose](#dan-moseley)       | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/CoreFxPlan.md) |  |
| [SymReader](#symreader)                       | [tmat](#tomas-matousek)       | On Track  | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/symreader/issues/157) | ETA: End of November |
| [SymReader-Portable](#symreader-portable)     | [tmat](#tomas-matousek)       | On Track  | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/symreader-portable/issues/144) | ETA: End of November |
| [Templating](#templating)                     | [vramak](#vijay-ramakrishnan) | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             | [plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/TemplatingAndWebSdkPlan.md) |  |
| [Test-Templates](#test-templates)             | [sasin](#sarabjot-singh)      | On Track  | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 0 / 4             | [plan](https://github.com/dotnet/arcade/blob/master/Documentation/NetCore3EngineeringRepoStatus.md#test-templates) |  |
| [Toolset](#toolset)                           | [licavalc](#livar-cunha)      | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             |[plan](https://github.com/dotnet/cli/blob/master/Documentation/MigrationPlan/CLISDKPlan.md) | Will follow CLI - need plan to go green |
| [Visual FSharp](#visual-fsharp)               | [brettfo](#brett-forsgren)    | At risk  | ![at risk](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png) | 0 / 4             | [plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/VisualFSharp.md) |  |
| [WebSDK](#websdk)                             | [vramak](#vijay-ramakrishnan) | On Track | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 1 / 4             | [plan](https://github.com/dotnet/arcade/blob/master/Documentation/MigrationPlan/TemplatingAndWebSdkPlan.md) |  |
| [WinForms](#winforms)                         | [mmcgaw](#merrie-mcgaw)       | On Track  | ![on track](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | 0 / 4             | [plan](https://github.com/dotnet/winforms/blob/master/Documentation/MigrationPlan.md) |  |
| [WPF](#wpf)                                   | [vatsan-madhavan](#vatsan-madhavan)  | At risk  | ![at risk](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png) | 0 / 4             | | New repo, so understanding requirements better - Need plan |

| Status   | Description |
| -------- | ----------- |
| Complete | All work streams are complete |
| On track | Work streams are on track for completion by the target completion date or exceptions are understood / acceptable |
| At risk  | One or more work streams are not on track to be completed by the target completion date and may impact business decisions |

| Risk Assessment                                                                           | Description |
|:-----------------------------------------------------------------------------------------:| ----------- |
| ![positive](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png) | Plan in place and project is on track to complete |
| ![at risk](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png) | No plan yet, or there are significant risks to completing on time |
| ![negative](http://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png)   | It is likely the needed work will not complete in time, or there is no plan and the work is large/complex |

---

## Repo Status (grouped by owners)

### Brett Forsgren

#### Visual FSharp

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/731)         | November 2018 (end)  | In progress   | Half-way done, need to finish migration to .NET Core 2 tools, first. |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/729) | December 2018 (mid)  | Not scheduled | Will be started immediately after the above. |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/728)       | December 2018 (mid)  | Not scheduled | Will be started immediately after the above. |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/730)       | January 2019         | Not scheduled | Lowest on our priority list, but should be reasonably fast once the above are complete. |

---

### Jared Parsons

#### Roslyn

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/637)         | October 2018         | In progress   | Waiting on spanish leg OS|
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/639) | November 2018        | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/640)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/638)       |                      | Not scheduled | No plans at this time |

---

### Livar Cunha

#### CLI

Owners: licavalc

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/655)         | ?                    | In progress   | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/653) | ?                    | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/652)       | ?                    | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/654)       | ?                    | In progress   | |

#### CLICommandLineParser

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/741)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/739) | October 2018         | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/738)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/740)       | October 2018         | Completed     | |

#### CLI-Migrate

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/736)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/734) | September 2018       | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/733)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/735)       | September 2018       | Completed     | |

#### Core-Sdk

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/696)         |                      | Not scheduled | Will follow Toolset repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/694) |                      | Not scheduled | Will follow Toolset repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/693)       |                      | Not scheduled | Will follow Toolset repo |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/695)       |                      | Not scheduled | Will follow Toolset repo |

#### SDK

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/650)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/648) | September 2018       | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/647)       | September 2018       | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/649)       |                      | Not scheduled | |

#### Toolset

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| Using Azure DevOps for CI - links to be provided by livar                        |                      | Not scheduled | Will follow CLI |
| Using shared toolset (Arcade SDK) - links to be provided by livar                |                      | Not scheduled | Will follow CLI |
| Engineering dependency flow - links to be provided by livar                      |                      | Not scheduled | Will follow CLI |
| Internal builds from dnceng - links to be provided by livar                      |                      | Not scheduled | Will follow CLI |

---

### Mark Wilkie

#### Arcade

| Work stream                            | Est. Completion Date | Status    | Notes |
| -------------------------------------- |:--------------------:| --------- | ----- |
| Using Azure DevOps for CI              |                      | Completed | |
| Using shared toolset (Arcade SDK)      |                      | Completed | |
| Engineering dependency flow            |                      | Completed | |
| Internal builds from dnceng            |                      | Completed | |

---

### Merrie McGaw

#### Winforms

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/706)         |         12/4         | In progress   |       |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/704) |         12/4         | In progress   |       |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/703)       |         12/4         | In progress   |       |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/705)       |         12/4         | In progress   |       |

---

### Vatsan Madhavan

#### WPF

The following work items are used to track work completion:

- [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/701)

- [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/699)

- [Engineering dependency flow](https://github.com/dotnet/arcade/issues/698)

- [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/700)


The Arcade migration plan will be completed in two phases:

- Phase 1: Migration of dotnet-wpf Repo

- Phase 2: Migration of dotnet-trusted Repo 


At the end of each phase, full migration would be completed for the corresponding repo, which includes the use of Azure DevOps, shared toolset (Arcade SDK), engineering dependency flow, and use of dnceng based internal builds. 



| Phase                                                | Est. Completion Date | Status                                                       | Notes                                |
| ---------------------------------------------------- | -------------------- | ------------------------------------------------------------ | ------------------------------------ |
| 1. Migration of dotnet-wpf Repo and its internal clone | Dec 4, 2018          | The dotnet-wpf repo will be set-up on dnceng using the shared toolset (Arcade SDK), and will use Azure DevOps. This work is currently being prototyped and is expected to be deployed by Dec 4th. | No known project risks at this time. |
| 2. Migration of dotnet-trusted Repo                   | Jan 31, 2019        | New C++/CLI support for .NET Core is being added in Dev16 Preview 2. We are testing this in Nov/Dec 2018, and will be prototyping our repo migration to shared toolset (Arcade SDK) during this time. The actual repo migration will happen in Jan 2019 on dnceng. At the end of this, we expect to be using Azure DevOps as well.|   There are some inherent risks here because we depend on untested/new techologies - notably C++/CLI support in Dev16. We also depend on the fact that this will get into Dev16 Preview 2 without delays or major bugs, and that we can set up Azure DevOps and setup to depend on Dev16 Preview 2.|



---

### Nate McMaster

#### AspNetCore

<https://github.com/aspnet/AspNetCore>

| Work stream                                                                      | Est. Completion Date | Status      | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/676)         | ?                    | Scheduled   | Planning to start mid-November |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/673) | ?                    | Scheduled   | Planning to start mid-November |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/673)       | ?                    | Scheduled   | Planning to start mid-November |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/675)       | ?                    | In progress | |

<a id="efcore"></a>

#### EntityFrameworkCore

<https://github.com/aspnet/EntityFrameworkCore>

| Work stream                                                                       | Est. Completion Date | Status      | Notes |
| --------------------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/1053)         | ?                    | Scheduled   | Planning to start mid-November |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/1051) | ?                    | Scheduled   | Planning to start mid-November |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/1050)       | ?                    | Scheduled   | Planning to start mid-November |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/1052)       | ?                    | In progress | |

#### Extensions

<https://github.com/aspnet/Extensions>

| Work stream                                                                       | Est. Completion Date | Status      | Notes |
| --------------------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/1048)         | ?                    | Scheduled   | Planning to start mid-November |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/1046) | ?                    | Scheduled   | Planning to start mid-November |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/1045)       | ?                    | Scheduled   | Planning to start mid-November |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/1047)       | ?                    | In progress | |

---

### Rainer Sigwald

#### MSBuild

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/726)         |                      | Not scheduled | Investigating requirements |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/724) |                      | Not scheduled | Investigating requirements |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/723)       |                      | Not scheduled | Investigating requirements |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/725)       |                      | Not scheduled | Investigating requirements |

---

### Russ Keldorph

#### CoreClr

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/645)         |                      | In progress | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/643) |                      | In progress | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/642)       |                      | In progress | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/644)       |                      | In progress | |

---

### Tomas Matousek

#### SymReader

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/666)         |                      | Not scheduled | Will follow Roslyn |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/664) |                      | Not scheduled | Will follow Roslyn |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/663)       |                      | Not scheduled | Will follow Roslyn |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/665)       |                      | Not scheduled | No plans at this time |

#### SymReader-Portable

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/671)         |                      | Not scheduled | Will follow Roslyn |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/669) |                      | Not scheduled | Will follow Roslyn |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/668)       |                      | Not scheduled | Will follow Roslyn |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/670)       |                      | Not scheduled | No plans at this time |

---

### Vijay Ramakrishnan

#### Templating

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/716)         |                      | Completed |  |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/714) | November 2018        | scheduled | @Joeloff will be working on this |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/713)       | December 2018        | scheduled | @Joeloff will be working on this |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/715)       | December 2018        | scheduled | @Joeloff will be working on this |

#### WebSdk

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/721)         |                      | Completed |  |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/719) | November 2018        | scheduled | @Joeloff will be working on this |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/718)       | December 2018        | scheduled | @Joeloff will be working on this |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/720)       | December 2018        | scheduled | @Joeloff will be working on this |

---

### Sarabjot Singh

#### Test-Templates

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/661)         |     November         | Scheduled     | Will follow Templating repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/658) |     November         | Scheduled     | Will follow Templating repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/657)       |     December         | Scheduled     | Will follow Templating repo |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/659)       |     December         | Scheduled     | Will follow Templating repo |

---

### Dan Moseley

#### CoreFx

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/686)         |                      | Not scheduled | Will follow Standard repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/684) | December 2018        | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/683)       | October 2018         | Completed | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/685)       |                      | Not scheduled | Will follow Standard repo |

#### Core-Setup

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/681)         |                      | Not scheduled | Will follow Standard repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/679) |                      | Not scheduled | Will follow Standard repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/678)       | October 2018         | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/680)       |                      | Not scheduled | Will follow Standard repo |

#### Standard

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/691)         |                      | Not scheduled | wtgodbe to work on this after Arcade SDK adoption |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/639) |                      | In progress   | wtgodbe is working on this |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/688)       | October 2018         | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/690)       |                      | Not scheduled | |
