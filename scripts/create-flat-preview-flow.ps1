param
(
    [Parameter(Mandatory=$true)][string]$RuntimeChannel,
    [Parameter(Mandatory=$true)][string]$SdkChannel,
    [Parameter(Mandatory=$true)][string]$RuntimeBranch,
    [Parameter(Mandatory=$true)][string]$SdkBranch,
    [Parameter(Mandatory=$true)][string]$VSChannel,
    [Parameter(Mandatory=$true)][string]$VSBranch
)

$vmr = "https://github.com/dotnet/dotnet"

function MakeDefaultChannel($repo, $branch, $channel)
{
    Write-Host "Making default channel - $repo @ $branch -> $channel"
    & darc add-default-channel --repo "$repo" --branch "$branch" --channel "$channel" --quiet
}

function DisableFlow($channel)
{
    Write-Host "Disabling flow on $channel"
    & darc subscription-status --disable --channel "$channel" --quiet --exact
}

function AddForwardFlow($sourceRepo, $sourceChannel, $targetVmr, $mapping, $targetBranch, $frequency)
{
    Write-Host "Adding forward flow - $sourceRepo @ $sourceChannel -> $targetVmr @ $targetBranch ($frequency)"
    & darc add-subscription --source-repo "$sourceRepo" --channel "$sourceChannel" --target-repo "$targetVmr" --target-branch "$targetBranch" --source-enabled true --target-directory $mapping --update-frequency "$frequency" --quiet --no-trigger --standard-automerge
}

function AddBatchedForwardFlow($sourceRepo, $sourceChannel, $targetVmr, $mapping, $targetBranch, $frequency)
{
    Write-Host "Adding forward flow - $sourceRepo @ $sourceChannel -> $targetVmr @ $targetBranch ($frequency)"
    & darc add-subscription --source-repo "$sourceRepo" --channel "$sourceChannel" --target-repo "$targetVmr" --target-branch "$targetBranch" --source-enabled true --target-directory $mapping --update-frequency "$frequency" --quiet --no-trigger --batchable
}

function AddBatchedMergePolicy($targetRepo, $targetBranch)
{
    Write-Host "Setting batched merge policy for $targetRepo @ $targetBranch"
    & darc set-repository-policies --repo "$targetRepo" --branch "$targetBranch" --standard-automerge --quiet
}

function AddBackwardsFlow($sourceVmr, $sourceChannel, $targetRepo, $mapping, $targetBranch, $excludedAssets, $frequency)
{
    Write-Host "Adding backflow - $sourceVmr @ $sourceChannel -> $targetRepo @ $targetBranch ($frequency)"
    & darc add-subscription --source-repo "$sourceVmr" --channel "$sourceChannel" --target-repo "$targetRepo" --target-branch "$targetBranch" --source-enabled true --source-directory $mapping --excluded-assets $excludedAssets --update-frequency "$frequency" --quiet --no-trigger --standard-automerge
}

function AddBatchedMergePolicy($targetRepo, $targetBranch)
{
    Write-Host "Setting batched merge policy for $targetRepo @ $targetBranch"
    & darc set-repository-policies --repo "$targetRepo" --branch "$targetBranch" --standard-automerge --quiet
}

Write-Host "Making default channels for runtime repos"
MakeDefaultChannel https://dev.azure.com/dnceng/internal/_git/dotnet-wpf-int $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/aspnetcore $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/efcore $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/emsdk $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/icu "dotnet/$RuntimeBranch" $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/runtime $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/windowsdesktop $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/wpf $RuntimeBranch $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/winforms $RuntimeBranch $RuntimeChannel

Write-Host "Making default channels for SDK repos"
MakeDefaultChannel https://github.com/dotnet/sdk $SdkBranch $SdkChannel
MakeDefaultChannel https://github.com/dotnet/templating $SdkBranch $SdkChannel

Write-Host "Making default channels for tooling repos"
MakeDefaultChannel https://github.com/NuGet/NuGet.Client "dev" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/fhsarp "release/dev$VSBranch" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/msbuild "vs$VSBranch" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/razor "release/$VSBranch" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/roslyn "release/$VSBranch" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/vstest "rel/$VSBranch" $SdkChannel

MakeDefaultChannel https://github.com/dotnet/diagnostics "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/deployment-tools "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/cecil "main" $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/command-line-api "main" $RuntimeChannel
MakeDefaultChannel https://github.com/dotnet/roslyn-analyzers "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/scenario-tests "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/source-build-externals "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/source-build-reference-packages "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/sourcelink "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/symreader "main" $SdkChannel
MakeDefaultChannel https://github.com/dotnet/xdt "main" $SdkChannel

Write-Host "Making default channel for the VMR"
MakeDefaultChannel $vmr $SdkBranch $SdkChannel

Write-Host "Setting the default branch policy in the VMR for batched subscriptions"
AddBatchedMergePolicy $vmr $SdkBranch

Write-Host "Making forward flow subscriptions for runtime repos"
AddForwardFlow https://github.com/dotnet/runtime $RuntimeChannel $vmr runtime $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/aspnetcore $RuntimeChannel $vmr aspnetcore $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/efcore $RuntimeChannel $vmr efcore $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/emsdk $RuntimeChannel $vmr emsdk $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/icu $RuntimeChannel $vmr icu $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/windowsdesktop $RuntimeChannel $vmr windowsdesktop $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/winforms $RuntimeChannel $vmr winforms $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/wpf $RuntimeChannel wpf $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/cecil $RuntimeChannel cecil $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/command-line-api $RuntimeChannel $vmr command-line-api $SdkBranch EveryBuild

Write-Host "Making forward flow subscriptions for sdk repos"
AddBatchedForwardFlow https://github.com/dotnet/sdk $SdkChannel sdk $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/templating $SdkChannel templating $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/scenario-tests $SdkChannel scenario-tests $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/source-build-externals $SdkChannel source-build-externals $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/source-build-reference-packages $SdkChannel source-build-reference-packages $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/roslyn-analyzers $SdkChannel roslyn-analyzers $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/sourcelink $SdkChannel sourcelink $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/symreader $SdkChannel symreader $vmr $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/xdt $SdkChannel xdt $vmr $SdkBranch EveryBuild

Write-Host "Making forward flow subscriptions for tooling repos"
AddBatchedForwardFlow https://github.com/NuGet/NuGet.Client $VSChannel $vmr nuget-client $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/fsharp $VSChannel $vmr fsharp $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/msbuild $VSChannel $vmr msbuild $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/razor $VSChannel $vmr razor $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/roslyn $VSChannel $vmr roslyn $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/vstest $VSChannel $vmr roslyn $SdkBranch EveryBuild

Write-Host "Adding merge policy for batched forward flow subscriptions"
AddBatchedMergePolicy $vmr $SdkBranch

#Don't know if these are sdk or runtime channels
AddForwardFlow https://github.com/dotnet/deployment-tools $SdkChannel $vmr deployment-tools $SdkBranch EveryBuild
AddForwardFlow https://github.com/dotnet/diagnostics $SdkChannel $vmr diagnostics $SdkBranch EveryBuild

Write-Host "Making backwards flow subscriptions for runtime repos"
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/runtime runtime $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/aspnetcore aspnetcore $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/efcore efcore $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/emsdk emsdk $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/icu icu "dotnet/$RuntimeBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/windowsdesktop windowsdesktop $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/winforms winforms $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/wpf wpf $RuntimeBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/cecil cecil main EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/command-line-api command-line-api main EveryBuild

Write-Host "Making backwards flow subscriptions for sdk repos"
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/sdk sdk $SdkBranch EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/templating templating $SdkBranch EveryBuild

Write-Host "Making backwards flow subscriptions for tooling repos"
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/fsharp fsharp "release/dev$VSBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/msbuild msbuild "vs$VSBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/razor razor "release/$VSBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/roslyn roslyn "release/$VSBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/vstest vstest "rel/$VSBranch" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/NuGet/NuGet.Client nuget-client "dev" EveryBuild

AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/deployment-tools deployment-tools main EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/diagnostics diagnostics main EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/source-build-externals source-build-externals "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/source-build-reference-packages source-build-reference-packages "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/roslyn-analyzers roslyn-analyzers "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/sourcelink sourcelink "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/symreader symreader "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/xdt xdt "main" EveryBuild
AddBackwardsFlow $vmr $SdkChannel https://github.com/dotnet/scenario-tests scenario-tests "main" EveryBuild