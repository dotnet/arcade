param
(
    [Parameter(Mandatory=$true)][string]$RuntimeChannel,
    [Parameter(Mandatory=$true)][string]$SdkChannel,
    [Parameter(Mandatory=$true)][string]$InternalRuntimeChannel,
    [Parameter(Mandatory=$true)][string]$InternalSdkChannel,
    [Parameter(Mandatory=$true)][ValidateSet('Preview','Preview+')][string]$FlowType,
    [switch]$TriggerSubscriptions
)

Write-Host "Switching flow to $FlowType"

switch ( $FlowType )
{
    'Preview' {
        # Switching to public flow is easy. Enable all the subscriptions in the public runtime and SDK channels
        # and disable any on the internal channels
        & darc subscription-status --disable --channel $InternalRuntimeChannel --quiet
        & darc subscription-status --disable --channel $InternalSdkChannel --quiet
        & darc subscription-status --enable --channel $RuntimeChannel --quiet
        & darc subscription-status --enable --channel $SdkChannel --quiet

        # Trigger public subscriptions
        if ($TriggerSubscriptions) {
            & darc trigger-subscriptions --channel $RuntimeChannel --quiet
            & darc trigger-subscriptions --channel $SdkChannel --quiet
        }
    }
    'Preview+' {
        # Switching to internal flow is a little more subtle. Enable the internal subscriptions,
        # then disable the corresponding public subscriptions, which is a subset of the full public channel
        & darc subscription-status --enable --channel $InternalRuntimeChannel --quiet
        & darc subscription-status --enable --channel $InternalSdkChannel --quiet

        & darc subscription-status --disable --source-repo https://github.com/dotnet/runtime --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/winforms --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/wpf --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/windowsdesktop --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/aspnetcore --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/efcore --channel $RuntimeChannel --quiet
        & darc subscription-status --disable --source-repo https://github.com/dotnet/sdk --channel $SdkChannel --quiet

        # Trigger internal subscriptions
        if ($TriggerSubscriptions) {
            & darc trigger-subscriptions --channel $InternalRuntimeChannel --quiet
            & darc trigger-subscriptions --channel $InternalSdkChannel --quiet
        }
    }
    default { Write-Error "Unknown flow type $FlowType" }
}