<#
This script is used as a payload of Helix jobs that execute Android workloads through XHarness on Windows systems.
This is used as the entrypoint of the work item so that XHarness failures can be detected and (when appropriate)
cause the work item to retry and reboot the Helix agent the work is running on.

Currently no special functionality is needed beyond causing infrastructure retry and reboot if the emulators
or devices have trouble, but to add more Helix-specific Android XHarness behaviors, this is one extensibility point.
#>

param (
    [Parameter(Mandatory)]
    [string]$app,
    [Parameter(Mandatory)]
    [string]$timeout,
    [Parameter(Mandatory)]
    [string]$package_name,
    [Parameter()]
    [int]$expected_exit_code = 0,
    [Parameter()]
    [string]$device_output_path = $null,
    [Parameter()]
    [string]$instrumentation = $null
)

$ErrorActionPreference="Stop"

[Diagnostics.CodeAnalysis.SuppressMessageAttribute("PSUseDeclaredVarsMoreThanAssignments", "")] # Variable used in sourced script
$output_directory=$Env:HELIX_WORKITEM_UPLOAD_ROOT

# The xharness alias
function xharness() {
    dotnet exec $Env:XHARNESS_CLI_PATH @args
}

# Act out the actual commands
. "$PSScriptRoot\command.ps1"

$exit_code=$LASTEXITCODE

# ADB_DEVICE_ENUMERATION_FAILURE
if ($exit_code -eq 85) {
    $ErrorActionPreference="Continue"
    Write-Error "Encountered ADB_DEVICE_ENUMERATION_FAILURE. This is typically not a failure of the work item. We will run it again and reboot this computer to help its devices"
    Write-Error "If this occurs repeatedly, please check for architectural mismatch, e.g. sending x86 or x86_64 APKs to an arm64_v8a-only queue."
    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart')"
}

# PACKAGE_INSTALLATION_FAILURE
if ($exit_code -eq 78) {
    $ErrorActionPreference="Continue"
    Write-Error "Encountered PACKAGE_INSTALLATION_FAILURE. This is typically not a failure of the work item. We will try it again on another Helix agent"
    Write-Error "If this occurs repeatedly, please check for architectural mismatch, e.g. requesting installation on arm64_v8a-only queue for x86 or x86_64 APKs."
    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
}

exit $exit_code
