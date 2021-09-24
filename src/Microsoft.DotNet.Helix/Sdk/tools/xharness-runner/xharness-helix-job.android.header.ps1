<#
This script is used as a payload of Helix jobs that execute Android workloads through XHarness on Windows systems.
This file is a header of script that gets populated with user's custom commands.

This script is separate as it is executed with a timeout in its own process.
#>

param (
    [Parameter(Mandatory)]
    [string]$output_directory,
    [Parameter(Mandatory)]
    [string]$app,
    [Parameter(Mandatory)]
    [string]$timeout,
    [Parameter()]
    [string]$package_name = $null,
    [Parameter()]
    [int]$expected_exit_code = 0,
    [Parameter()]
    [string]$device_output_path = $null,
    [Parameter()]
    [string]$instrumentation = $null
)

$ErrorActionPreference="Continue"

# The xharness alias
function xharness() {
    dotnet exec $Env:XHARNESS_CLI_PATH @args
}

# User can call this when they detect a problem they think is caused by the infrastructure
function report_infrastructure_failure($message) {
    Write-Output "Infrastructural problem reported by the user, requesting retry+reboot: $message"

    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart')"
}

