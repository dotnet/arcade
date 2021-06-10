param
(
    [Parameter(Mandatory=$true)][string]$RuntimeChannel,
    [Parameter(Mandatory=$true)][string]$SdkChannel,
    [Parameter(Mandatory=$true)][string]$RuntimeBranch,
    [Parameter(Mandatory=$true)][string]$SdkBranch,
    [Parameter(Mandatory=$true)][string]$VSChannel
)

function MakeDefaultChannel($repo, $branch, $channel)
{
    Write-Host "Making default channel - $repo @ $branch -> $channel"
    & darc add-default-channel --repo "$repo" --branch "$branch" --channel "$channel" --quiet
}

function AddFlow($sourceRepo, $sourceChannel, $targetRepo, $targetBranch, $frequency)
{
    Write-Host "Adding flow - $sourceRepo @ $sourceChannel -> $targetRepo @ $targetBranch ($frequency)"
    & darc add-subscription --source-repo "$sourceRepo" --channel "$sourceChannel" --target-repo "$targetRepo" --target-branch "$targetBranch" --update-frequency "$frequency" --quiet --no-trigger --standard-automerge
}

function AddArcadeFlow($targetRepo, $targetBranch)
{
    AddFlow https://github.com/dotnet/arcade ".NET Eng - Latest" $targetRepo $targetBranch None
}

# Make default channels

Write-Host "Making default channels for runtime repos"
MakeDefaultChannel https://dev.azure.com/dnceng/internal/_git/dotnet-wpf-int $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/aspnetcore $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/efcore $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/emsdk $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/icu $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/runtime $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/windowsdesktop $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/wpf $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/winforms $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/mono/linker $RuntimeBranch $RuntimeChannel

Write-Host "Making default channels for SDK repos"
MakeDefaultChannel https://github.com/dotnet/installer $SdkBranch $SdkChannel
MakeDefaultChannel https://github.com/dotnet/sdk $SdkBranch $SdkChannel
MakeDefaultChannel https://github.com/dotnet/roslyn-analyzers $SdkBranch $SdkChannel
MakeDefaultChannel https://github.com/dotnet/templating $SdkBranch $SdkChannel

# Make dependency flow

Write-Host "Add arcade flow"
AddArcadeFlow https://dev.azure.com/dnceng/internal/_git/dotnet-wpf-int $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/aspnetcore $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/efcore $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/emsdk $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/icu $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/runtime $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/windowsdesktop $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/wpf $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/winforms $RuntimeBranch
AddArcadeFlow https://github.com/mono/linker $RuntimeBranch
AddArcadeFlow https://github.com/dotnet/installer $SdkBranch
AddArcadeFlow https://github.com/dotnet/sdk $SdkBranch
AddArcadeFlow https://github.com/dotnet/roslyn-analyzers $SdkBranch
AddArcadeFlow https://github.com/dotnet/templating $SdkBranch

Write-Host "Add runtime -> runtime flow"
AddFlow https://dev.azure.com/dnceng/internal/_git/dotnet-wpf-int $RuntimeChannel https://github.com/dotnet/wpf $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/efcore $RuntimeChannel https://github.com/dotnet/aspnetcore $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/emsdk $RuntimeChannel https://github.com/dotnet/aspnetcore $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/emsdk $RuntimeChannel https://github.com/dotnet/runtime $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/icu $RuntimeChannel https://github.com/dotnet/runtime $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/runtime $RuntimeChannel https://github.com/dotnet/aspnetcore $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/runtime $RuntimeChannel https://github.com/dotnet/efcore $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/runtime $RuntimeChannel https://github.com/dotnet/winforms $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/winforms $RuntimeChannel https://github.com/dotnet/wpf $RuntimeBranch EveryBuild
AddFlow https://github.com/dotnet/wpf $RuntimeChannel https://github.com/dotnet/windowsdesktop $RuntimeBranch EveryBuild
AddFlow https://github.com/mono/linker $RuntimeChannel https://github.com/dotnet/runtime $RuntimeBranch EveryBuild

Write-Host "Add runtime->sdk flow"
AddFlow https://github.com/dotnet/aspnetcore $RuntimeChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/windowsdesktop $RuntimeChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/runtime $RuntimeChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/mono/linker $RuntimeChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild

Write-Host "Add sdk->sdk flow"
AddFlow https://github.com/dotnet/sdk $SdkChannel https://github.com/dotnet/installer $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/roslyn-analyzers $SdkChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/templating $SdkChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild

Write-Host "Add tooling->sdk flow"
AddFlow https://github.com/nuget/nuget.client $VSChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/roslyn $VSChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/fsharp $VSChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild
AddFlow https://github.com/dotnet/msbuild $VSChannel https://github.com/dotnet/sdk $SdkBranch EveryBuild



