# Servicing Readiness Adoption status

## Primary Deliverables

1. Transition to [YAML stages based publishing](./CorePackages/YamlStagesPublishing.md) and the post-build YAML template.

Target completion date is 8/13/2019.

## Status Overview

| Repo                       | Owner            | Status   | Risk Assessment                                                                      | Notes|
| ---------------------------| ---------------- | -------- |--------------------------------------------------------------------------------------| -----|
| Arcade                     | mawilkie         | Complete | ✔️ | |
| Arcade-Services            | mawilkie         | Complete | ✔️ | |
| Arcade-Validation          | mawilkie         | Complete | ✔️ | |
| aspnet-AspNetCore          | dougbu    | On track | ➖ |  In PR https://github.com/aspnet/AspNetCore/pull/13040 |
| aspnet-AspNetCore-Tooling  | dougbu    | Complete | ✔️ | |
| aspnet-EntityFramework6    | dougbu/wtgodbe    | Complete | ✔️ | |
| aspnet-EntityFrameworkCore | dougbu/wtgodbe    | Complete | ✔️ | |
| aspnet-Blazor              | dougbu    | Complete | ✔️ | |
| aspnet-Extensions          | dougbu/wtgodbe    | Complete | ✔️ | |
| CLI                        | licavalc         | Complete | ✔️ |  No plan available |
| CLICommandLineParser       | licavalc         | N/A | |  This repo is not being developed anymore. We are taking a pinned version of it |
| CoreClr                    | russellk/arobins         | On track | ➖ | Expected completion: 8/9 |
| CoreFx                     | danmose/safern   | Complete | ✔️ | SourceLink disabled: https://github.com/dotnet/arcade/issues/3603 |
| IoT                        | joperezr         | Complete | ✔️ | |
| Core-SDK                   | licavalc         | At risk | ❌ |  We need clarity on how to do the blob storage publishing with YAML stages. |
| Core-Setup                 | dleeapho         | At risk | ❌ |  Expected completion 8/21 |
| FSharp                     | brettfo          | Complete | ✔️ | |
| MSBuild                    | licavalc         | At risk | ❌ |  No ETA yet. Investigation under way. |
| nuget-NugetClient          | dtivel           | At risk | ❌ |  No plan available |
| Roslyn                     | jaredpar         | At risk | ✔️ |  Complete with source link disabled |
| SDK                        | licavalc         | In progress |  |  Running into issues with signing and asset publishing |
| SourceLink                 | tmat             | Complete | ✔️ | |
| Standard                   | danmose/wigodbe  | Complete | ✔️ | |
| SymReader                  | tmat             | Complete | ✔️ | |
| SymReader-Portable         | tmat             | Complete | ✔️ | |
| Templating                 | vramak           | Complete | ✔️ | |
| Test-Templates             | sasin            | At risk | ❌ |  No plan available |
| Toolset                    | licavalc/riarenas| Complete | ✔️ | |
| WebSDK                     | vramak           | Complete | ✔️ | |
| WinForms                   | mmcgaw           | Complete | ✔️ | |
| WPF                        | vatsan-madhavan  | Complete | ✔️ | Some reliability problems being observed, for e.g., https://github.com/dotnet/arcade/issues/3609| 

| Status     | Description |
| ---------- | ----------- |
| Complete ✔️| All work streams are complete |
| On track ➖| Work streams are on track for completion by the target completion date or exceptions are understood / acceptable |
| At risk  ❌| One or more work streams are not on track to be completed by the target completion date and may impact business decisions |
