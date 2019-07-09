# .NET Core 3 RestoreSources update

We decided to use Azure DevOps artifact internal feeds to store the assets we produce during internal builds. While testing that things
worked, we found out that `nuget restore` doesn't support internal feeds that are defined in `RestoreSources` and it expects that ALL the
sources are specified in `NuGet.config`

## What is needed?

We need repo owners to complete a two-part work explained bellow in the following steps:

1) Copy the list of feeds from `Version.props` file to `NuGet.config` file in the repository root. Keep the feeds in `Version.props` file
2) Merge this change
3) Update the Status of your repo in the table bellow to "Part 1 complete"
4) Once all repos have completed Part 1, https://github.com/dotnet/arcade/pull/3041 will be merged and changes propagated to all repos
5) Arcade working group member will inform that #4 is complete and will inform the working group that Part 2 can be done
6) Delete the code that sets `RestoreSources` in `Version.props`
7) Update the Status of your repo in the table bellow to "Part 2 complete"

| Repo                       | Owner            |  Status     | Notes              |
| ---------------------------| ---------------- | ---------   | -------------------|
| Arcade                     | mawilkie         |             |                    |
| Arcade-Validation          | mawilkie         |             |                    |
| Arcade-Services            | mawilkie         |             |                    |
| Standard                   | danmose          |             |                    |
| SymReader                  | tmat             |             |                    |
| SymReader-Portable         | tmat             |             |                    |
| CoreFx                     | danmose          |             |                    |
| Templating                 | vramak           |             |                    |
| Test-Templates             | singhsarab       |             |                    |
| Toolset                    | licavalc         |             |                    |
| CoreClr                    | russellk         |             |                    |
| WebSDK                     | vramak           |             |                    |
| WinForms                   | mmcgaw           |             |                    |
| WPF                        | vatsan-madhavan  |             |                    |
| aspnet-EntityFrameworkCore | namc             |             |                    |
| aspnet-Extensions          | namc             |             |                    |
| aspnet-AspNetCore-Tooling  | namc             |             |                    |
| CLI                        | licavalc         |             |                    |
| CLI-Migrate                | licavalc         |             |                    |
| CLICommandLineParser       | licavalc         |             |                    |
| nuget-NugetClient          | dtivel           |             |                    |
| aspnet-AspNetCore          | namc             |             |                    |
| Core-Setup                 | dleeapho         |             |                    |
| MSBuild                    | licavalc         |             |                    |
| Roslyn                     | jaredpar         |             |                    |
| Visual-FSharp              | brettfo          |             |                    |
| Core-SDK                   | licavalc         |             |                    |

For any question, please ping @tmat or @jcagme