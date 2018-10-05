# .NET Core 3 Engineering transition status

- [Azure DevOps CI status](#using-azure-devops-for-ci)
- [Arcade SDK adoption status](#using-shared-toolset-arcade-sdk)
- [Engineering dependency flow status](#engineering-dependency-flow)
- [Internal builds from dev.azure.com/dnceng status](#internal-builds-from-dnceng)

Target completion date for these workstreams is 12/14/2018.

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