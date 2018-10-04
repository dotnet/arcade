# .NET Core 3 Engineering transition status

- [Azure DevOps CI status](#using-azure-devops-for-ci)
- [Arcade SDK adoption status](#using-shared-toolset-arcade-sdk)
- [Engineering dependency flow status](#engineering-dependency-flow)
- [Internal builds from dev.azure.com/dnceng status](#internal-builds-from-dnceng)

Target completion date for these workstreams is 12/14/2018.

## Using Azure DevOps for CI

| Repo                                                                | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/676)              | ?                    |             | |
| [CLI](https://github.com/dotnet/arcade/issues/655)                  | ?                    |             | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/741) | September 2018       | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/736)          | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/645)              | ?                    | In progress | |
| [CoreFx](https://github.com/dotnet/arcade/issues/686)               | ?                    |             | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/696)             | ?                    |             | |
| [Core-Setup](https://github.com/dotnet/arcade/issues/681)           | ?                    |             | |
| [MSBuild](https://github.com/dotnet/arcade/issues/726)              | ?                    | In progress | CI builds do not import base.yml |
| [Roslyn](https://github.com/dotnet/arcade/issues/637)               | October 2018         | In progress | One Spanish test leg still uses Jenkins |
| [SDK](https://github.com/dotnet/arcade/issues/650)                  | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/691)             | ?                    |             | |
| [SymReader](https://github.com/dotnet/arcade/issues/666)            | ?                    |             | |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/671)   | ?                    |             | |
| [Templating](https://github.com/dotnet/arcade/issues/716)           | ?                    | Scheduling  | Starting planning |
| [Test-Templates](https://github.com/dotnet/arcade/issues/661)       | ?                    |             | Will follow Templating |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/731)        | ?                    |             | CI builds do not import base.yml |
| [WebSDK](https://github.com/dotnet/arcade/issues/721)               | ?                    | Scheduling  | Starting planning |
| [WinForms](https://github.com/dotnet/arcade/issues/706)             | ?                    | Scheduling  | Starting planning |
| [WPF](https://github.com/dotnet/arcade/issues/701)                  | ?                    | Scheduling  | Starting planning |

## Using shared toolset (Arcade SDK)

| Repo                                                                | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/674)              | ?                    |             | |
| [CLI](https://github.com/dotnet/arcade/issues/653)                  | ?                    |             | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/739) | October 2018         | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/734)          | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/643)              | ?                    | In progress | |
| [CoreFx](https://github.com/dotnet/arcade/issues/684)               | December 2018        | In progress | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/694)             | ?                    |             | |
| [Core-Setup](https://github.com/dotnet/arcade/issues/679)           | January 2019         |             | Will follow CoreFx |
| [MSBuild](https://github.com/dotnet/arcade/issues/724)              | ?                    |             | |
| [Roslyn](https://github.com/dotnet/arcade/issues/639)               | November 2018        | In progress | |
| [SDK](https://github.com/dotnet/arcade/issues/648)                  | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/689)             | October 2018         | In progress | |
| [SymReader](https://github.com/dotnet/arcade/issues/664)            | ?                    |             | Will follow Roslyn |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/669)   | ?                    |             | Will follow Roslyn |
| [Templating](https://github.com/dotnet/arcade/issues/714)           | ?                    | Scheduling  | Starting planning |
| [Test-Templates](https://github.com/dotnet/arcade/issues/658)       | ?                    |             | Will follow Templating |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/729)        | ?                    |             | |
| [WebSDK](https://github.com/dotnet/arcade/issues/719)               | ?                    |             | Starting planning |
| [WinForms](https://github.com/dotnet/arcade/issues/704)             | ?                    |             | Starting planning |
| [WPF](https://github.com/dotnet/arcade/issues/699)                  | ?                    |             | Starting planning |

## Engineering dependency flow

| Repo                                                                | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/673)              | ?                    |             | |
| [CLI](https://github.com/dotnet/arcade/issues/652)                  | ?                    |             | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/738) | ?                    |             | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/733)          | ?                    |             | |
| [CoreClr](https://github.com/dotnet/arcade/issues/642)              | ?                    |             | |
| [CoreFx](https://github.com/dotnet/arcade/issues/683)               | October 2018         | Completed   | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/693)             | ?                    |             | |
| [Core-Setup](https://github.com/dotnet/arcade/issues/678)           | October 2018         | Completed   | |
| [MSBuild](https://github.com/dotnet/arcade/issues/723)              | ?                    |             | |
| [Roslyn](https://github.com/dotnet/arcade/issues/640)               | ?                    |             | |
| [SDK](https://github.com/dotnet/arcade/issues/647)                  | ?                    |             | |
| [Standard](https://github.com/dotnet/arcade/issues/688)             | October 2018         | Completed   | |
| [SymReader](https://github.com/dotnet/arcade/issues/663)            | ?                    |             | |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/668)   | ?                    |             | |
| [Templating](https://github.com/dotnet/arcade/issues/713)           | ?                    | Scheduling  | Starting planning |
| [Test-Templates](https://github.com/dotnet/arcade/issues/657)       | ?                    |             | |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/728)        | ?                    |             | |
| [WebSDK](https://github.com/dotnet/arcade/issues/718)               | ?                    | Scheduling  | Starting planning |
| [WinForms](https://github.com/dotnet/arcade/issues/703)             | ?                    | Scheduling  | Starting planning |
| [WPF](https://github.com/dotnet/arcade/issues/698)                  | ?                    | Scheduling  | Starting planning |

## Internal builds from DncEng

| Repo                                                                | Est. Completion Date | Status      | Notes |
| ------------------------------------------------------------------- |:--------------------:| ----------- | ----- |
| Arcade                                                              | September 2018       | Completed   | |
| [ASP.Net](https://github.com/dotnet/arcade/issues/675)              | ?                    | In progress | |
| [CLI](https://github.com/dotnet/arcade/issues/654)                  | ?                    |             | |
| [CLICommandLineParser](https://github.com/dotnet/arcade/issues/740) | October 2018         | Completed   | |
| [CLI-Migrate](https://github.com/dotnet/arcade/issues/735)          | September 2018       | Completed   | |
| [CoreClr](https://github.com/dotnet/arcade/issues/644)              | ?                    |             | |
| [CoreFx](https://github.com/dotnet/arcade/issues/685)               | ?                    |             | |
| [Core-SDK](https://github.com/dotnet/arcade/issues/695)             | ?                    |             | |
| [Core-Setup](https://github.com/dotnet/arcade/issues/680)           | ?                    |             | |
| [MSBuild](https://github.com/dotnet/arcade/issues/725)              | ?                    |             | |
| [Roslyn](https://github.com/dotnet/arcade/issues/638)               | ?                    |             | No plans at this time |
| [SDK](https://github.com/dotnet/arcade/issues/649)                  | September 2018       | Completed   | |
| [Standard](https://github.com/dotnet/arcade/issues/690)             | ?                    |             | |
| [SymReader](https://github.com/dotnet/arcade/issues/665)            | ?                    |             | |
| [SymReader-Portable](https://github.com/dotnet/arcade/issues/670)   | ?                    |             | |
| [Templating](https://github.com/dotnet/arcade/issues/715)           | ?                    | Scheduling  | Starting planning |
| [Test-Templates](https://github.com/dotnet/arcade/issues/659)       | ?                    |             | Will follow Templating |
| [Visual FSharp](https://github.com/dotnet/arcade/issues/730)        | ?                    |             | |
| [WebSDK](https://github.com/dotnet/arcade/issues/720)               | ?                    | Scheduling  | Starting planning |
| [WinForms](https://github.com/dotnet/arcade/issues/705)             | ?                    | Scheduling  | Starting planning |
| [WPF](https://github.com/dotnet/arcade/issues/700)                  | ?                    | Scheduling  | Starting planning |