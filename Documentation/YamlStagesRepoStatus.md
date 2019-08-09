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
| aspnet-AspLabs             | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| aspnet-AspNetCore          | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| aspnet-AspNetCore-Tooling  | dougbu    | Complete | ✔️ | |
| aspnet-EntityFramework6    | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| aspnet-EntityFrameworkCore | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| aspnet-Blazor              | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| aspnet-Extensions          | dougbu    | At risk | ❌ |  Expected completion 8/11 |
| CLI                        | licavalc         | At risk | ❌ |  No plan available |
| CLICommandLineParser       | licavalc         | At risk | ❌ |  No plan available |
| CoreClr                    | russellk/arobins         | On track | ➖ | Expected completion: 8/9 |
| CoreFx                     | danmose/safern   | Complete | ✔️ | SourceLink disabled: https://github.com/dotnet/arcade/issues/3603 |
| IoT                        | joperezr         | Complete | ✔️ | |
| Core-SDK                   | licavalc         | At risk | ❌ |  No plan available |
| Core-Setup                 | dleeapho         | At risk | ❌ |  Expected completion 8/21 |
| FSharp                     | brettfo          | Complete | ✔️ | |
| MSBuild                    | licavalc         | At risk | ❌ |  No plan available |
| nuget-NugetClient          | dtivel           | At risk | ❌ |  No plan available |
| Roslyn                     | jaredpar         | At risk | ❌ |  Expected completion 8/15 |
| SDK                        | licavalc         | At risk | ❌ |  No plan available |
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
