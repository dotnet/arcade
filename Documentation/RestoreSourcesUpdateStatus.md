# .NET Core 3 RestoreSources update

We decided to use Azure Artifacts feeds to store the assets we produce during internal builds. Testing revealed that nuget restore doesn't 
support private feeds that are defined in RestoreSources, and instead it expects ALL sources to be specified in NuGet.config.

## What is needed?

We need repo owners to complete a two-part work explained bellow in the following parts:

### Part 1

1) Copy the list of feeds from `Version.props` file to `NuGet.config` file in the repository root. Keep the feeds in `Version.props` file
2) Update the Status of your repo in the table bellow to "Part 1 complete"

### Part 2

1) Once all repos have completed Part 1, https://github.com/dotnet/arcade/pull/3041 will be merged and changes propagated to all repos
2) Arcade working group member will inform that #4 is complete and will inform the working group that Part 2 can be done
3) Delete the code that sets `RestoreSources` in `Version.props`
4) Update the Status of your repo in the table bellow to "Part 2 complete"

| Repo                       | Owner            |  Status           | Notes              |
| ---------------------------| ---------------- | ---------         | -------------------|
| Arcade                     | mawilkie         | Part 2 complete   |                    |
| Arcade-Validation          | mawilkie         | Part 1 complete   |                    |
| Arcade-Services            | mawilkie         | Part 1 complete   |                    |
| Standard                   | danmose          | Part 1 complete   | No change needed   |
| SymReader                  | tmat             | Part 1 complete   |                    |
| SymReader-Portable         | tmat             | Part 1 complete   |                    |
| CoreFx                     | danmose          | Part 1 complete   | No change needed   |
| Templating                 | vramak           | Part 1 complete   |                    |
| Test-Templates             | singhsarab       | Part 1 complete   |                    |
| Toolset                    | licavalc         | Part 1 complete   |                    |
| CoreClr                    | russellk         | Part 1 complete   | No change needed   |
| WebSDK                     | vramak           | Part 1 complete   |                    |
| WinForms                   | mmcgaw           | Part 1 complete   | No change needed   |
| WPF                        | vatsan-madhavan  | Part 1 complete   | No change needed   |
| aspnet-EntityFrameworkCore | namc             | Part 1 complete   |                    |
| aspnet-Extensions          | namc             | Part 1 complete   | No change needed   |
| aspnet-AspNetCore-Tooling  | namc             | Part 1 complete   |                    |
| aspnet-Blazor              | namc             | Part 1 complete   | No change needed   |
| aspnet-EntityFramework6    | namc             | Part 1 complete   | No change needed   |
| aspnet-AspLabs             | namc             | Part 1 complete   |                    |
| CLI                        | licavalc         | Part 1 complete   |                    |
| CLICommandLineParser       | licavalc         | Part 1 complete   | No change needed   |
| nuget-NugetClient          | dtivel           | Part 1 complete   | No change needed   |
| aspnet-AspNetCore          | namc             | Part 1 complete   |                    |
| Core-Setup                 | dleeapho         | Part 1 complete   |                    |
| MSBuild                    | licavalc         | Part 1 complete   |                    |
| Roslyn                     | jaredpar         | Part 1 complete   |                    |
| Visual-FSharp              | brettfo          | Part 1 complete   |                    |
| Core-SDK                   | licavalc         | Part 1 complete   |                    |

For any question, please ping @tmat or @jcagme
