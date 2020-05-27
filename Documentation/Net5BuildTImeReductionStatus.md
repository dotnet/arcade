# .NET 5 Build Time Reduction Status

The goal for .NET 5 is to bring the E2E build time under two hours. To this end, a number of things should be done to help bring
the time for each repo's build under a prescribed budget. This document tracks those changes. For more information on the changes, see
[.NET 5 Build Shape](https://github.com/dotnet/arcade/blob/master/Documentation/Net5Builds.md).

## Budgets

The expected potential critical paths of the build after consolidation is complete are:

- runtime -> aspnetcore -> sdk -> installer
- runtime -> winforms -> wpf -> windowsdesktop -> sdk -> installer
- runtime -> wpf-int -> wpf -> windowsdesktop -> sdk -> installer

Based on the rough amount of build work done in each repo, budgets for each build leg, to fit within the overall 2 hour official time goal, have been set as follows:

| Repo                                 | Budget (mins) |
| ------------------------------------ | ------------- |
| runtime                              | 45            |
| aspnetcore                           | 45            |
| sdk + installer                      | 30            |
| winforms + wpf + windowsdesktop (wd) | 45            |
| wpf-int + wpf + windowsdesktop (wd)  | 45            |

### Feasibility

Based on the previous calculations of potential build times done when deciding what to do about repo consolidation,
it is believed that these numbers are within the realm of possibility. Signing, tests and validation have an outsized impact
on build, and VM size can potentially be increased to improve parallelism within a single build. For more information, see
[.NET 5 Build Shape](https://github.com/dotnet/arcade/blob/master/Documentation/Net5Builds.md)

## Current Status

- **NA = Not applicable** - Not applicable to this repo
- **NYA = Not yet applicable** - Requires work from Eng teams before implementation is possible
- **NR = Not required** - This work is not necessarily required to meet the 2 hour build time. For instance,
  repos that are disconnecting from the main flow and shipping only as disconnected components may choose to keep
  signing and validation within their builds.

*Note that if some fields are NYA (signing/validation), the "Within budget" field is a rough estimation
of whether the repo will be within budge once those items are complete.*


| Repo               | Owner    | Consolidated/Removed from flow | Moved tests | Post-signing | Post-validation | Budget (mins)                   | Within budget |
| ------------------ | -------- | ------------------------------ | ----------- | ------------ | --------------- | ------------------------------- | ------------- |
| websdk             | vramak   | ![][red]                       | NA          | NA           | NA              | NA                              | NA            |
| aspnetcore         | kevinpi  | NA                             | ![][red]    | NYA          | NYA             | 45                              | ![][red]      |
| aspnetcore-tooling | kevinpi  | ![][red]                       | NA          | NA           | NA              | NA                              | NA            |
| efcore             | kevinpi  | ![][green]                     | ![][red]    | NR           | NR              | NA                              | NA            |
| extensions         | kevinpi  | ![][green]                       | ![][red]    | NR           | NR              | NA                              | NA            |
| installer          | marcpop    | NA                             | ![][green]    | NYA          | NYA             | 30 (w/sdk)                      | ![][red]      |
| runtime            | jaredpar | ![][green]                     | ![][green]  | NYA          | NYA             | 45                              | ![][red]      |
| sdk                | marcpop    | NA                             | ![][green]  | NYA          | NYA             | 30 (w/installer)                | ![][red]      |
| templating         | joaguila | NA                             | ![][red]    | NYA          | NYA             | NA                              | NA            |
| windowsdesktop     | kevinpi | NA                             | ![][red]    | NYA          | NYA             | 45 (w/winforms + wpf)           | ![][red]      |
| winforms           | kevinpi | NA                             | ![][red]    | NYA          | NYA             | 45 (w/wpf + wd)                 | ![][red]      |
| wpf-int            | fabiant | NA                             | ![][red]    | NYA          | NYA             | 45 (w/wpf + wd)                 | ![][red]      |
| wpf                | fabiant | NA                             | ![][red]    | NYA          | NYA             | 45 (w/winforms or wpf-int + wd) | ![][red]      |

[red]: https://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Red.png
[green]: https://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Green.png
[yello]: https://individual.icons-land.com/IconsPreview/Sport/PNG/16x16/Ball_Yellow.png
