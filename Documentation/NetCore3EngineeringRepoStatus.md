# .NET Core 3 Engineering transition status

Target completion date for these workstreams is 12/14/2018.

<<<<<<< HEAD
| Repo                                          | Owner                         | Completion Status | Notes |
| --------------------------------------------- | ----------------------------- |:-----------------:| ----- |
| [Arcade](#arcade)                             | [mawilkie](#mark-wilkie)      | 4 / 4             | |
| [ASP.Net](#aspnet)                            | [namc](#nate-mcmaster)        | 0 / 4             | Internal builds in dnceng work is in progress |
| [CLI](#cli)                                   | [licavalc](#livar-cunha)      | 0 / 4             | In progress |
| [CLICommandLineParser](#clicommandlineparser) | [licavalc](#livar-cunha)      | 3 / 4             | dependency flow still pending |
| [CLI-Migrate](#climigrate)                    | [licavalc](#livar-cunha)      | 3 / 4             | dependency flow still pending |
| [CoreClr](#coreclr)                           | [russellk](#russ-keldorph)    | 0 / 4             | |
| [CoreFx](#corefx)                             | [wesh](#wes-haggard)          | 1 / 4             | |
| [Core-SDK](#coresdk)                          | [licavalc](#livar-cunha)      | 0 / 4             | Will start following Toolset repo |
| [Core-Setup](#coresetup)                      | [wesh](#wes-haggard)          | 1 / 4             | |
| [MSBuild](#msbuild)                           | [raines](#rainer-sigwald)     | 0 / 4             | Investigating requirements |
| [Roslyn](#roslyn)                             | [jaredpar](#jared-parsons)    | 0 / 4             | |
| [SDK](#sdk)                                   | [licavalc](#livar-cunha)      | 3 / 4             | dependency flow still pending |
| [Standard](#standard)                         | [wesh](#wes-haggard)          | 1 / 4             | wtgodbe is working on this |
| [SymReader](#symreader)                       | [tmat](#tomas-matousek)       | 0 / 4             | Will follow Roslyn |
| [SymReader-Portable](#symreader-portable)     | [tmat](#tomas-matousek)       | 0 / 4             | Will follow Roslyn |
| [Templating](#templating)                     | [vramak](#vijay-ramakrishnan) | 0 / 4             | |
| [Test-Templates](#test-templates)             | [vramak](#vijay-ramakrishnan) | 0 / 4             | |
| [Toolset](#toolset)                           | [licavalc](#livar-cunha)      | 0 / 4             | Will follow CLI
| [Visual FSharp](#visual-fsharp)               | [brettfo](#brett-forsgren)    | 0 / 4             | |
| [WebSDK](#websdk)                             | [vramak](#vijay-ramakrishnan) | 0 / 4             | |
| [WinForms](#winforms)                         | [mmcgaw](#merrie-mcgaw)       | 0 / 4             | |
| [WPF](#wpf)                                   | [mmcgaw](#merrie-mcgaw)       | 0 / 4             | |

---

## Brett Forsgren

### Visual FSharp

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/731)         |                      | Not scheduled | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/729) |                      | Not scheduled | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/728)       |                      | Not scheduled | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/730)       |                      | Not scheduled | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |

---

## Jared Parsons

### Roslyn

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/637)         | October 2018         | In progress   | Waiting on spanish leg OS|
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/639) | November 2018        | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/640)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/638)       |                      | Not scheduled | No plans at this time |

---

## Livar Cunha

### CLI

Owners: licavalc

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/655)         | ?                    | In progress   | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/653) | ?                    | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/652)       | ?                    | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/654)       | ?                    | In progress   | |

### CLICommandLineParser

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/741)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/739) | October 2018         | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/738)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/740)       | October 2018         | Completed     | |

### CLI-Migrate

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/736)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/734) | September 2018       | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/733)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/735)       | September 2018       | Completed     | |

### Core-Sdk

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/696)         |                      | Not scheduled | Will follow Toolset repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/694) |                      | Not scheduled | Will follow Toolset repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/693)       |                      | Not scheduled | Will follow Toolset repo |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/695)       |                      | Not scheduled | Will follow Toolset repo |

### SDK

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/650)         | September 2018       | Completed     | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/648) | September 2018       | Completed     | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/647)       | September 2018       | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/649)       |                      | Not scheduled | |

### Toolset

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| Using Azure DevOps for CI - links to be provided by livar                        |                      | Not scheduled | Will follow CLI |
| Using shared toolset (Arcade SDK) - links to be provided by livar                |                      | Not scheduled | Will follow CLI |
| Engineering dependency flow - links to be provided by livar                      |                      | Not scheduled | Will follow CLI |
| Internal builds from dnceng - links to be provided by livar                      |                      | Not scheduled | Will follow CLI |

---

## Mark Wilkie

### Arcade

| Work stream                            | Est. Completion Date | Status    | Notes |
| -------------------------------------- |:--------------------:| --------- | ----- |
| Using Azure DevOps for CI              |                      | Completed | |
| Using shared toolset (Arcade SDK)      |                      | Completed | |
| Engineering dependency flow            |                      | Completed | |
| Internal builds from dnceng            |                      | Completed | |

---

## Merrie McGaw

### Winforms

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/706)         |                      | Not scheduled | Looking at documentation / requirements|
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/704) |                      | Not scheduled | Looking at documentation / requirements|
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/703)       |                      | Not scheduled | Looking at documentation / requirements|
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/705)       |                      | Not scheduled | Looking at documentation / requirements|

### Wpf

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/701)         |                      | Not scheduled | Looking at documentation / requirements|
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/699) |                      | Not scheduled | Looking at documentation / requirements|
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/698)       |                      | Not scheduled | Looking at documentation / requirements|
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/700)       |                      | Not scheduled | Looking at documentation / requirements|

---

## Nate McMaster

### ASP.Net

| Work stream                                                                      | Est. Completion Date | Status      | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/676)         | ?                    | Scheduled   | Planning to start mid-November |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/673) | ?                    | Scheduled   | Planning to start mid-November |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/673)       | ?                    | Scheduled   | Planning to start mid-November |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/675)       | ?                    | In progress | |

---

## Rainer Sigwald

### MSBuild

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/726)         |                      | Not scheduled | Investigating requirements |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/724) |                      | Not scheduled | Investigating requirements |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/723)       |                      | Not scheduled | Investigating requirements |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/725)       |                      | Not scheduled | Investigating requirements |

---

## Russ Keldorph

### CoreClr

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/645)         |                      | In progress   | |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/643) |                      | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/642)       |                      | Not scheduled | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/644)       |                      | Not scheduled | |

---

## Tomas Matousek

### SymReader

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/666)         |                      | Not scheduled | Will follow Roslyn |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/664) |                      | Not scheduled | Will follow Roslyn |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/663)       |                      | Not scheduled | Will follow Roslyn |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/665)       |                      | Not scheduled | No plans at this time |

### SymReader-Portable

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/671)         |                      | Not scheduled | Will follow Roslyn |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/669) |                      | Not scheduled | Will follow Roslyn |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/668)       |                      | Not scheduled | Will follow Roslyn |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/670)       |                      | Not scheduled | No plans at this time |

---

## Vijay Ramakrishnan

### Templating

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/716)         |                      | Not scheduled | No resources available yet for scheduling |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/714) |                      | Not scheduled | No resources available yet for scheduling |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/713)       |                      | Not scheduled | No resources available yet for scheduling |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/715)       |                      | Not scheduled | No resources available yet for scheduling |

### Test-Templates

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/661)         |                      | Not scheduled | Will follow Templating repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/658) |                      | Not scheduled | Will follow Templating repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/657)       |                      | Not scheduled | Will follow Templating repo |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/659)       |                      | Not scheduled | Will follow Templating repo |

### WebSdk

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/721)         |                      | Not scheduled | No resources available yet for scheduling |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/719) |                      | Not scheduled | No resources available yet for scheduling |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/718)       |                      | Not scheduled | No resources available yet for scheduling |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/720)       |                      | Not scheduled | No resources available yet for scheduling |

---

## Wes Haggard

### CoreFx

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/686)         |                      | Not scheduled | Will follow Standard repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/684) | December 2018        | In progress   | |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/683)       | October 2018         | Completed | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/685)       |                      | Not scheduled | Will follow Standard repo |

### Core-Setup

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/681)         |                      | Not scheduled | Will follow Standard repo |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/679) |                      | Not scheduled | Will follow Standard repo |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/678)       | October 2018         | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/680)       |                      | Not scheduled | Will follow Standard repo |

### Standard

| Work stream                                                                      | Est. Completion Date | Status        | Notes |
| -------------------------------------------------------------------------------- |:--------------------:| ------------- | ----- |
| [Using Azure DevOps for CI](https://github.com/dotnet/arcade/issues/691)         |                      | Not scheduled | wtgodbe to work on this after Arcade SDK adoption |
| [Using shared toolset (Arcade SDK)](https://github.com/dotnet/arcade/issues/639) |                      | In progress   | wtgodbe is working on this |
| [Engineering dependency flow](https://github.com/dotnet/arcade/issues/688)       | October 2018         | Completed     | |
| [Internal builds from dnceng](https://github.com/dotnet/arcade/issues/690)       |                      | Not scheduled | |
=======
## Using Azure DevOps for CI

| Repo                                                                | Owner    | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- | -------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | mawilkie | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/676)              | namc     | ?                    | **Blocked** | Planning to start mid-November |
| [CLI](https://github.com/dotnet/arcade/issues/655)                  | licavalc | ?                    | In progress | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/741) | licavalc | September 2018       | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/736)          | licavalc | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/645)              | russellk | ?                    | In progress | |
| [CoreFx](https://github.com/dotnet/arcade/issues/686)               | wesh     | ?                    | **Blocked** | Will follow Standard |
| [Core-SDK](https://github.com/dotnet/arcade/issues/696)             | licavalc | ?                    | **Blocked** | Will follow Toolset |
| [Core-Setup](https://github.com/dotnet/arcade/issues/681)           | wesh     | ?                    | **Blocked** | Will follow Standard |
| [MSBuild](https://github.com/dotnet/arcade/issues/726)              | raines   | ?                    | **Blocked** | Investigating requirements |
| [Roslyn](https://github.com/dotnet/arcade/issues/637)               | jaredpar | October 2018         | In progress | One Spanish test leg still uses Jenkins |
| [SDK](https://github.com/dotnet/arcade/issues/650)                  | licavalc | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/691)             | wtgodbe  | ?                    | **Blocked** | Will begin working on this after Arcade SDK adoption |
| [SymReader](https://github.com/dotnet/arcade/issues/666)            | tmat     | ?                    | **Blocked** | Will follow Roslyn |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/671)   | tmat     | ?                    | **Blocked** | Will follow Roslyn |
| [Templating](https://github.com/dotnet/arcade/issues/716)           | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [Test-Templates](https://github.com/dotnet/arcade/issues/661)       | vramak   | ?                    | **Blocked** | Will follow Templating |
|  Toolset - link to be provided by livar                             | licavalc | ?                    | **Blocked** | Will follow CLI |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/731)        | brettfo  | ?                    | **Blocked** | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [WebSDK](https://github.com/dotnet/arcade/issues/721)               | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [WinForms](https://github.com/dotnet/arcade/issues/706)             | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements |
| [WPF](https://github.com/dotnet/arcade/issues/701)                  | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements |

## Using shared toolset (Arcade SDK)

| Repo                                                                | Owner    | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- | -------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | mawilkie | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/674)              | namc     | ?                    | **Blocked**  | Planning to start mid-November |
| [CLI](https://github.com/dotnet/arcade/issues/653)                  | licavalc | ?                    | In progress | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/739) | licavalc | October 2018         | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/734)          | licavalc | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/643)              | russellk | ?                    | In progress | |
| [CoreFx](https://github.com/dotnet/arcade/issues/684)               | wesh     | December 2018        | In progress | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/694)             | licavalc | ?                    | **Blocked** | Will follow Toolset |
| [Core-Setup](https://github.com/dotnet/arcade/issues/679)           | wesh     | January 2019         | **Blocked** | Will follow Standard |
| [MSBuild](https://github.com/dotnet/arcade/issues/724)              | raines   | ?                    | **Blocked** | Investigating requirements |
| [Roslyn](https://github.com/dotnet/arcade/issues/639)               | jaredpar | November 2018        | In progress | |
| [SDK](https://github.com/dotnet/arcade/issues/648)                  | licavalc | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/689)             | wtgodbe  | October 2018         | In progress | |
| [SymReader](https://github.com/dotnet/arcade/issues/664)            | tmat     | ?                    | **Blocked** | Will follow Roslyn |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/669)   | tmat     | ?                    | **Blocked** | Will follow Roslyn |
| [Templating](https://github.com/dotnet/arcade/issues/714)           | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [Test-Templates](https://github.com/dotnet/arcade/issues/658)       | vramak   | ?                    | **Blocked** | Will follow Templating |
|  Toolset - link to be provided by livar                             | licavalc | ?                    | **Blocked** | Will follow CLI |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/729)        | brettfo  | ?                    | **Blocked** | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [WebSDK](https://github.com/dotnet/arcade/issues/719)               | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [WinForms](https://github.com/dotnet/arcade/issues/704)             | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements  |
| [WPF](https://github.com/dotnet/arcade/issues/699)                  | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements  |

## Engineering dependency flow

| Repo                                                                | Owner    | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- | -------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | mawilkie | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/673)              | namc     | ?                    | **Blocked** | Planning to start mid-November |
| [CLI](https://github.com/dotnet/arcade/issues/652)                  | licavalc | ?                    | **Blocked** | Not yet scheduled |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/738) | licavalc | ?                    | **Blocked** | Not yet scheduled |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/733)          | licavalc | ?                    | **Blocked** | Not yet scheduled |
| [CoreClr](https://github.com/dotnet/arcade/issues/642)              | russellk | ?                    | **Blocked** | Not yet scheduled |
| [CoreFx](https://github.com/dotnet/arcade/issues/683)               | wesh     | October 2018         | Completed   | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/693)             | licavalc | ?                    | **Blocked** | Not yet scheduled |
| [Core-Setup](https://github.com/dotnet/arcade/issues/678)           | wesh     | October 2018         | Completed   | |
| [MSBuild](https://github.com/dotnet/arcade/issues/723)              | raines   | ?                    | **Blocked** | Investigating requirements |
| [Roslyn](https://github.com/dotnet/arcade/issues/640)               | jaredpar | ?                    | **Blocked** | Not yet scheduled |
| [SDK](https://github.com/dotnet/arcade/issues/647)                  | licavalc | ?                    | **Blocked** | Not yet scheduled |
| [Standard](https://github.com/dotnet/arcade/issues/688)             | wtgodbe  | October 2018         | Completed   | |
| [SymReader](https://github.com/dotnet/arcade/issues/663)            | tmat     | ?                    | **Blocked** | Not yet scheduled |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/668)   | tmat     | ?                    | **Blocked** | Not yet scheduled |
| [Templating](https://github.com/dotnet/arcade/issues/713)           | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [Test-Templates](https://github.com/dotnet/arcade/issues/657)       | vramak   | ?                    | **Blocked** | Will follow Templating |
|  Toolset - link to be provided by livar                             | licavalc | ?                    | **Blocked** | Will follow CLI |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/728)        | brettfo  | ?                    | **Blocked** | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [WebSDK](https://github.com/dotnet/arcade/issues/718)               | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [WinForms](https://github.com/dotnet/arcade/issues/703)             | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements |
| [WPF](https://github.com/dotnet/arcade/issues/698)                  | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements |

## Internal builds from DncEng

| Repo                                                                | Owner    | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- | -------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | mawilkie | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/675)              | namc     | ?                    | In progress | |
| [CLI](https://github.com/dotnet/arcade/issues/654)                  | licavalc | ?                    | In progress | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/740) | licavalc | October 2018         | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/735)          | licavalc | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/644)              | russellk | ?                    | **Blocked** | Not yet scheduled |
| [CoreFx](https://github.com/dotnet/arcade/issues/685)               | wesh     | ?                    | **Blocked** | Will follow Standard |
| [Core-SDK](https://github.com/dotnet/arcade/issues/695)             | licavalc | ?                    | **Blocked** | Will follow Toolset |
| [Core-Setup](https://github.com/dotnet/arcade/issues/680)           | wesh     | ?                    | **Blocked** | Will follow Standard |
| [MSBuild](https://github.com/dotnet/arcade/issues/725)              | raines   | ?                    | **Blocked** | Investigating requirements |
| [Roslyn](https://github.com/dotnet/arcade/issues/638)               | jaredpar | ?                    | **Blocked** | No plans at this time |
| [SDK](https://github.com/dotnet/arcade/issues/649)                  | licavalc | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/690)             | wtgodbe  | ?                    | **Blocked** | Working on Arcade SDK work first |
| [SymReader](https://github.com/dotnet/arcade/issues/665)            | tmat     | ?                    | **Blocked** | No plans at this time |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/670)   | tmat     | ?                    | **Blocked** | No plans at this time |
| [Templating](https://github.com/dotnet/arcade/issues/715)           | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [Test-Templates](https://github.com/dotnet/arcade/issues/659)       | vramak   | ?                    | **Blocked** | Will follow Templating |
|  Toolset - link to be provided by livar                             | licavalc | ?                    | **Blocked** | Will follow CLI |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/730)        | brettfo  | ?                    | **Blocked** | Currently migrating to .NET Core 2 tools, scheduling this work is pending completion of that work |
| [WebSDK](https://github.com/dotnet/arcade/issues/720)               | vramak   | ?                    | **Blocked** | No resources available yet for scheduling |
| [WinForms](https://github.com/dotnet/arcade/issues/705)             | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements  |
| [WPF](https://github.com/dotnet/arcade/issues/700)                  | mmcgaw   | ?                    | **Blocked** | Looking at documentation / requirements  |
>>>>>>> 504a862aa4e98755eb4c5bff93f06579de891104
