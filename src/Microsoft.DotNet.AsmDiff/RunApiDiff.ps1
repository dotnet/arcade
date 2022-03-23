# This script allows running API-diff to generate the dotnet/core report that compares the APIs introduced between two previews, in the format expected for publishing in the dotnet/core repo.

Param (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern("\d+\.\d")]
        [string]
        $PreviousDotNetVersion # 7.0, 8.0, 9.0, ...
    ,
        [Parameter(Mandatory=$true)]
        [string]
        $PreviousPreviewVersion # preview1, preview2, RC1, RC2, or even " "
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [ValidatePattern("\d+\.\d")]
        [string]
        $CurrentDotNetVersion # 7.0, 8.0, 9.0, ...
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentPreviewVersion # preview1, preview2, rc1, rc2
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentDotNetNumberAndPreviewFriendlyName # "7.0 Preview 1", "7.0 RC1"
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CoreRepo #"D:\\core"
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $ArcadeRepo #"D:\\arcade"
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $TmpFolder #"D:\tmp"
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
)

### Functions

Function VerifyPathOrExit
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $path
    )

    If (-Not (Test-Path -Path $path))
    {
        Write-Color red "The path does not exist: $path"
        Exit
    }
}

Function RemoveFolder
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $path
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    If (Test-Path -Path $path)
    {
        Write-Color yellow "Removing existing folder: $path"

        If (-Not $Debugging)
        {
            Remove-Item -Recurse -Path $path
        }
    }
}
Function CreateFolder
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $path
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    RemoveFolder $path $Debugging

    Write-Color white "Creating new folder: $path"
    If (-Not $Debugging)
    {
        New-Item -ItemType Directory -Path $path
    }
}

Function VerifyCountDlls
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $path
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    VerifyPathOrExit $path

    If (-Not $Debugging)
    {
       $count=(Get-ChildItem -Path $path -Filter "*.dll" | Measure-Object).Count
        If ($count -eq 0)
        {
            Write-Color red "There are no DLL files inside the folder."
            Exit
        }
    }
}

Function RunAsmDiff
{
    Param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $AsmDiffExe
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $TableOfContentsFilePath
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $BeforeFolder
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $AfterFolder
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    VerifyPathOrExit $AsmDiffExe
    VerifyPathOrExit $BeforeFolder
    VerifyPathOrExit $AfterFolder

    If (Test-Path -Path $TableOfContentsFilePath)
    {
        Write-Color yellow "Deleting existing table of contents file..."
        If (-Not $Debugging)
        {
            Remove-Item -Path $TableOfContentsFilePath
        }
    }
    # Arguments currently used:
    # -r: Include members, types, and namespaces that were removed.
    # -a: Include members, types and namespaces that were added.
    # -c: Included members, types and namespaces that were changed.
    # -itc: Include table of contents.
    # -cfn: Create files per namespace.
    # -adm: Forces showing all members of a type that was added or removed.
    # -hbm: Highlight members that are interface implementations or overrides of a base member.
    # -da: Enables diffing of the attributes as well.
    # -w markdown: Type of diff writer to use.
    # -o <path>: Output file path.
    # -os <path>: Path to the old assembly set (baseline).
    # -ns <path>: Path to the new assembly set.

    # All arguments:
    # https://github.com/dotnet/arcade/blob/main/src/Microsoft.DotNet.AsmDiff/Program.cs

    $Command="$AsmDiffExe -r -a -c -itc -cfn -adm -hbm -da -w markdown -o $TableOfContentsFilePath -os $BeforeFolder -ns $AfterFolder"

    Run-Command -Command $Command -Debugging $Debugging
}

Function ReplaceTitle
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PreviousDotNetVersion
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentDotNetVersion
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $TableOfContentsFilePath
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    VerifyPathOrExit $TableOfContentsFilePath

    $CorrectTitle="# API Difference ${PreviousDotNetVersion} vs ${CurrentDotNetVersion}"

    Write-Color white "Replacing title of table of contents with correct one: $TableOfContentsFilePath"
    $UpdatedTableOfContents = .{
        $CorrectTitle
        Get-Content $TableOfContentsFilePath | Select-Object -Skip 1
    }
    If (-Not $Debugging)
    {
        Set-Content -Path $TableOfContentsFilePath -Value $UpdatedTableOfContents
    }
}

Function CreateReadme
{
    Param
    (
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $PreviewFolderPath
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentDotNetVersion
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentPreviewVersion
    ,
        [Parameter(Mandatory=$true)]
        [ValidateNotNullOrEmpty()]
        [string]
        $CurrentDotNetNumberAndPreviewFriendlyName
    ,
        [Parameter(Mandatory=$false)]
        [boolean]
        $Debugging = $false
    )

    If (-Not $Debugging)
    {
        $ReadmePath=[IO.Path]::Combine($PreviewFolderPath, "README.md")
        If (Test-Path -Path $ReadmePath)
        {
            Remove-Item -Path $ReadmePath
        }
        New-Item -ItemType File $ReadmePath

        $DotNetNumberAndPreview="$CurrentDotNetVersion-$CurrentPreviewVersion"

        $FullCurrentDotNetVersionFile="$DotNetNumberAndPreview.md"

        Add-Content $ReadmePath "# .NET $CurrentDotNetNumberAndPreviewFriendlyName API Changes"
        Add-Content $ReadmePath ""
        Add-Content $ReadmePath "The following API changes were made in .NET $($CurrentDotNetNumberAndPreviewFriendlyName):"
        Add-Content $ReadmePath ""
        Add-Content $ReadmePath "- [Microsoft.NETCore.App](./Microsoft.NETCore.App/$FullCurrentDotNetVersionFile)"
        Add-Content $ReadmePath "- [Microsoft.AspNetCore.App](./Microsoft.AspNetCore.App/$FullCurrentDotNetVersionFile)"
        Add-Content $ReadmePath "- [Microsoft.WindowsDesktop.App](./Microsoft.WindowsDesktop.App/$FullCurrentDotNetVersionFile)"
    }
}

### Execution

## Check folders passed as parameters

VerifyPathOrExit $CoreRepo
VerifyPathOrExit $ArcadeRepo
VerifyPathOrExit $TmpFolder


## Check that the before and after folders exist and they have DLLs inside

# NETCore
$NETCoreBeforeFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.NETCore.App.Before")
VerifyCountDlls $NETCoreBeforeFolder $Debugging

$NETCoreAfterFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.NETCore.App.After")
VerifyCountDlls $NETCoreAfterFolder $Debugging

# AspNetCore
$AspNetCoreBeforeFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.AspNetCore.App.Before")
VerifyCountDlls $AspNetCoreBeforeFolder $Debugging

$AspNetCoreAfterFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.AspNetCore.App.After")
VerifyCountDlls $AspNetCoreAfterFolder $Debugging

# WindowsDesktop
$WindowsDesktopBeforeFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.WindowsDesktop.App.Before")
VerifyCountDlls $WindowsDesktopBeforeFolder $Debugging

$WindowsDesktopAfterFolder = [IO.Path]::Combine($TmpFolder, "Microsoft.WindowsDesktop.App.After")
VerifyCountDlls $WindowsDesktopAfterFolder $Debugging


# Verify the AsmDiff project folder exists

$AsmDiffProjectPath = [IO.Path]::Combine($ArcadeRepo, "src", "Microsoft.DotNet.AsmDiff", "Microsoft.DotNet.AsmDiff.csproj")
VerifyPathOrExit $AsmDiffProjectPath


## Build the AsmDiff project if it hasn't been built yet

$AsmDiffArtifactsPath = [IO.Path]::Combine($ArcadeRepo ,"artifacts", "bin", "Microsoft.DotNet.AsmDiff")
$AsmDiffExe = [IO.Path]::Combine($AsmDiffArtifactsPath, "Release", "net6.0", "Microsoft.DotNet.AsmDiff.exe")
If (-Not (Test-Path -Path $AsmDiffExe))
{
    # Building the AsmDiff project

    Write-Color white "Building AsmDiff project"

    $BuildCommand="dotnet build -c release $AsmDiffProjectPath"
    Run-Command -Command $BuildCommand -Debugging $Debugging

    # Verifying expected output from building
    VerifyPathOrExit $AsmDiffArtifactsPath
    VerifyPathOrExit $AsmDiffExe
}


## Re-creating api-diff folder in core repo folder

$PreviewFolderPath = [IO.Path]::Combine($CoreRepo, "release-notes", $CurrentDotNetVersion, "preview", "api-diff", $CurrentPreviewVersion)
Write-Color white "Checking existing diff folder: $PreviewFolderPath"
CreateFolder $PreviewFolderPath $Debugging


## Creating subfolders

# NETCore
$NETCoreTargetFolder = [IO.Path]::Combine($PreviewFolderPath, "Microsoft.NETCore.App")
CreateFolder $NETCoreTargetFolder $Debugging

#AspNetCore
$AspNetCoreTargetFolder = [IO.Path]::Combine($PreviewFolderPath, "Microsoft.AspNetCore.App")
CreateFolder $AspNetCoreTargetFolder $Debugging

# WindowsDesktop
$WindowsDesktopTargetFolder = [IO.Path]::Combine($PreviewFolderPath, "Microsoft.WindowsDesktop.App")
CreateFolder $WindowsDesktopTargetFolder $Debugging


## Run the Asm-Diff commands

$TableOfContentsFile = "$CurrentDotNetVersion-$CurrentPreviewVersion.md"

$TableOfContentsFilePathNETCore = [IO.Path]::Combine($NETCoreTargetFolder, $TableOfContentsFile)
$TableOfContentsFilePathAspNetCore = [IO.Path]::Combine($AspNetCoreTargetFolder, $TableOfContentsFile)
$TableOfContentsFilePathWindowsDesktop = [IO.Path]::Combine($WindowsDesktopTargetFolder, $TableOfContentsFile)

RunAsmDiff $AsmDiffExe $TableOfContentsFilePathNETCore $NETCoreBeforeFolder $NETCoreAfterFolder $Debugging
RunAsmDiff $AsmDiffExe $TableOfContentsFilePathAspNetCore $AspNetCoreBeforeFolder $AspNetCoreAfterFolder $Debugging
RunAsmDiff $AsmDiffExe $TableOfContentsFilePathWindowsDesktop $WindowsDesktopBeforeFolder $WindowsDesktopAfterFolder $Debugging


## Replace the first line of the summmary files with the correct title

$FullPreviousDotNetVersion=$PreviousDotNetVersion
If (-Not [System.String]::IsNullOrWhiteSpace($PreviousPreviewVersion))
{
    $FullPreviousDotNetVersion="$PreviousDotNetVersion-$PreviousPreviewVersion"
}
$FullCurrentDotNetVersion=$CurrentDotNetVersion
If (-Not [System.String]::IsNullOrWhiteSpace($CurrentPreviewVersion))
{
    $FullCurrentDotNetVersion="$CurrentDotNetVersion-$CurrentPreviewVersion"
}

ReplaceTitle $FullPreviousDotNetVersion $FullCurrentDotNetVersion $TableOfContentsFilePathNETCore $Debugging
ReplaceTitle $FullPreviousDotNetVersion $FullCurrentDotNetVersion $TableOfContentsFilePathAspNetCore $Debugging
ReplaceTitle $FullPreviousDotNetVersion $FullCurrentDotNetVersion $TableOfContentsFilePathWindowsDesktop $Debugging

CreateReadme $PreviewFolderPath $CurrentDotNetVersion $CurrentPreviewVersion $CurrentDotNetNumberAndPreviewFriendlyName $Debugging