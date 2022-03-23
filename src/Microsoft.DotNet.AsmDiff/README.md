# Microsoft.DotNet.AsmDiff

AsmDiff is a command line tool which may be used to check the API changes between a two sets of .NET assemblies.

## Usage

### Required Options

- `-os|--OldSet` - Provide path to an assembly or directory for an assembly set to gather the old set of types. These types will be the baseline for the compare.
- `-ns|--NewSet` - Provide path to an assembly or directory for an assembly set to gather the new set of types. If this parameter is not provided the API's for the oldset will be printed instead of the diff.

### Additional Options

- `-nsn|--NewSetName` - Provide a name for the new set in output. If this parameter is not provided the file or directory name will be used.
- `-osn|--OldSetName` - Provide a name for the old set in output. If this parameter is not provided the file or directory name will be used.
- `-u|--Unchanged` - Include members, types, and namespaces that are unchanged.
- `-r|--Removed` - Include members, types, and namespaces that were removed. (default is removed and added)
- `-a|--Added` - Include members, types, and namespaces that were added. (default is removed and added)
- `-c|--Changed` - Include members, types, and namespaces that were changed. (default is removed and added)
- `-to|--TypesOnly` - Only show down to the type level not the member level.
- `-sr|--StrikeRemoved` - For removed API's also strike them out. This option currently only works with the HTML writer which is the default.
- `-da|--DiffAttributes` - Enables diffing of the attributes as well, by default all attributes are ignored. For CSV writer causes the assembly name to be included in the column for types.
- `-dai|--DiffAssemblyInfo` - Enables diffing of the assembly level information like version, key, etc.
- `-adm|--AlwaysDiffMembers` - By default if an entire class is added or removed we don't show the members, setting this option forces all the members to be shown instead.
- `-hbm|--HighlightBaseMembers` - Highlight members that are interface implementations or overrides of a base member.
- `-ft|--FlattenTypes` - Will flatten types so that all members available on the type show on the type not just the implemented ones.
- `-gba|--GroupByAssembly` - Group the differences by assembly instead of flattening the namespaces.
- `-eat|--ExcludeAddedTypes` - Do not show types that have been added to the new set of types.
- `-ert|--ExcludeRemovedTypes` - Do not show types that have been removed from the new set of types.
- `-iia|--IncludeInternalApis` - Include internal types and members as part of the diff.
- `-ipa|--IncludePrivateApis` - Include private types and members as part of the diff.
- `-w|--DiffWriter` - Type of difference writer to use, either CSharp code diffs or flat list of compat violations (default).
- `-s|--SyntaxWriter` - Specific the syntax writer type. Only used if the writer is CSDecl
- `-o|--OutFile` - Output file path. Default is the console.
- `-l|--Language` - Provide a languagetag for localized content. Currently language specific content is only available in Markdown for en and de. If this parameter is not provided the environments default language will be used. If a specific language is not supported english is the default language.

## Script

The [`RunApiDiff.ps1`](./RunApiDiff.ps1) script can generate an API comparison report for two specified .NET previews, in the format expected for publishing in the dotnet/core repo.

Instructions:

1. Clone this repo. Let's assume you clone it into `D:\arcade`.
2. Clone the dotnet/core repo. Let's assume you clone it into `D:\core`.
3. Create a temporary directory. Let's assume you create it in `D:\tmp`.
4. Inside the temporary directory, create 6 subdirectories (one pair for each SDK):

   - `Microsoft.AspNetCore.App.Before`
   - `Microsoft.AspNetCore.App.After`
   - `Microsoft.NETCore.App.Before`
   - `Microsoft.NETCore.App.After`
   - `Microsoft.WindowsDesktop.App.Before`
   - `Microsoft.WindowsDesktop.App.After`

5. For each one of the above SDKs, you will need to download the two nuget packages that contain the ref assemblies of the .NET versions you want to compare. For example, let's assume you want to compare `7.0-preview1` vs `7.0-preview2`. Then you have to open each one of these links:

    - https://www.nuget.org/packages/Microsoft.AspNetCore.App.Ref/
    - https://www.nuget.org/packages/Microsoft.WindowsDesktop.App.Ref/
    - https://www.nuget.org/packages/Microsoft.NETCore.App.Ref/

6. Inside each link, you have to click on the `Versions` tab, select the desired preview from the list, then click on `Download package` on the sidebar on the right. Do this for each pair of versions.
7. Change the extension of the files to zip. Open them with File Explorer. Navigate to the folder `ref\net7.0\`, select all the contents, then copy them and paste them into the appropriate `*.Before` or `*.After` directory. For example, the `7.0-preview1` nupkg for AspNet should go into `Microsoft.AspNetCore.App.Before` and the `7.0-preview2` nupkg should go into `Microsoft.AspNetCore.App.After`. Similar with the other two SDKs.
8. Run the script, specifying the desired value for the mandatory arguments:
    - `PreviousDotNetVersion`: Indicates the .NET version of the `Before` folders, in the `N.N` format. For this example, it's `7.0`.
    - `PreviousPreviewVersion`: Indicates the preview name of the `Before` folders, and it can be in the `previewN` or `RCN` formats. For this example, it's `preview1`.
    - `CurrentDotNetversion`: Indicates the .NET version of the `After` folders, in the `N.N` format. For this example, it's `7.0` as well.
    - `CurrentPreviewVersion`: Indicates the preview name of the `After` folders, in the `previewN` or `RCN` formats. For this example, it's `preview2`.
    - `CurrentDotNetNumberAndPreviewFriendlyName`: Indicates the friendly name of the current .NET version and preview name, without dashes, and without ".NET". This string will be printed as the header of the main `*.md` output readme file. For this example, we want this value to be `7.0 Preview 2`.
    - `CoreRepo`: The absolute path of the cloned core repo. For this example: `D:\core`.
    - `ArcadeRepo`: The absolute path of the cloned arcade repo. For this example: `D:\arcade`.
    - `TmpFolder`: The absolute path of the temporary directory containing the `Before` and `After` folders. For this example: `D:\tmp`.

Execution example:

```powershell
.\RunApiDiff.ps1 `
    -PreviousDotNetVersion 7.0 `
    -PreviousPreviewVersion preview1 `
    -CurrentDotNetVersion 7.0 `
    -CurrentPreviewVersion preview2 `
    -CurrentDotNetNumberAndPreviewFriendlyName "7.0 Preview 2" `
    -CoreRepo D:\core `
    -ArcadeRepo D:\arcade `
    -TmpFolder D:\tmp
```

Examples of what this script generates:

- PR comparing .NET 6.0 vs .NET 7.0 Preview1: https://github.com/dotnet/core/pull/7211
- PR comparing .NET 7.0 Preview1 vs Preview2: https://github.com/dotnet/core/pull/7307