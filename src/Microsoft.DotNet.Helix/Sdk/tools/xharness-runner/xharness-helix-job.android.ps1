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
    [int]$command_timeout, # in seconds
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

# Act out the actual commands
# We have to time constrain them to create buffer for the end of this script
$psinfo = [System.Diagnostics.ProcessStartInfo]::new()
$psinfo.FileName = "powershell"
$psinfo.Arguments = " -ExecutionPolicy ByPass -NoProfile -File `"$PSScriptRoot\command.ps1`" -output_directory `"$Env:HELIX_WORKITEM_UPLOAD_ROOT`" -app `"$app`" -timeout `"$timeout`" -package_name `"$package_name`" -expected_exit_code `"$expected_exit_code`" -device_output_path `"$device_output_path`" -instrumentation `"$instrumentation`""
$psinfo.RedirectStandardError = $false
$psinfo.RedirectStandardOutput = $false
$psinfo.UseShellExecute = $false

$process = [System.Diagnostics.Process]::new()
$process.StartInfo = $psinfo
$process.Start()

Wait-Process -InputObject $process -TimeOut $command_timeout -ErrorVariable ev -ErrorAction SilentlyContinue

if ($ev) {
    $exit_code = -3
    Stop-Process -InputObject $process -Force
    $process.WaitForExit()
    [Console]::Out.Flush()
    Write-Output "User command timed out after $command_timeout seconds!"
} else {
    $exit_code = $process.ExitCode
    Write-Output "User command ended with $exit_code"
}

$retry=$false
$reboot=$false

switch ($exit_code)
{
    # ADB_DEVICE_ENUMERATION_FAILURE
    85 {
        Write-Error "Encountered ADB_DEVICE_ENUMERATION_FAILURE. This is typically not a failure of the work item. We will run it again and reboot this computer to help its devices"
        Write-Error "If this occurs repeatedly, please check for architectural mismatch, e.g. sending x86 or x86_64 APKs to an arm64_v8a-only queue."
        $retry=$true
        $reboot=$true
        Break
    }

    # PACKAGE_INSTALLATION_FAILURE
    78 {
        Write-Error "Encountered PACKAGE_INSTALLATION_FAILURE. This is typically not a failure of the work item. We will try it again on another Helix agent"
        Write-Error "If this occurs repeatedly, please check for architectural mismatch, e.g. requesting installation on arm64_v8a-only queue for x86 or x86_64 APKs."
        $retry=$true
        Break
    }
}

if ($retry) {
    & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
}

if ($reboot) {
     & "$Env:HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart')"
}

exit $exit_code
