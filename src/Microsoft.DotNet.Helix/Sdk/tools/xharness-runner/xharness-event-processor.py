import getopt
import json
import os
import subprocess
import sys
from typing import Tuple

from helix.public import request_reboot, request_infra_retry, send_metric, send_metrics

### This script's purpose is to parse the diagnostics.json file produced by XHarness, evaluate it and send it to AppInsights
### The diagnostics.json file contains information about each XHarness command executed during the job
### In case of events that suggest infrastructure issues, we request a retry and for some reboot the agent

# Name of metrics we send (to Kusto)
EVENT_TYPE = 'MobileDeviceOperation'
OPERATION_METRIC_NAME = 'ExitCode'
DURATION_METRIC_NAME = 'Duration'
RETRY_METRIC_NAME = 'Retry'
REBOOT_METRIC_NAME = 'Reboot'
NETWORK_CONNECTIVITY_METRIC_NAME = 'NoInternet'

opts, args = getopt.gnu_getopt(sys.argv[1:], 'd:', ['diagnostics-data='])
opt_dict = dict(opts)

diagnostics_file = None

if '--data' in opt_dict:
    diagnostics_file = opt_dict['--data']
elif '-d' in opt_dict:
    diagnostics_file = opt_dict['-d']
else:
    diagnostics_file = os.getenv('XHARNESS_DIAGNOSTICS_PATH')

if not diagnostics_file:
    print('ERROR: Expected path to the diagnostics JSON file generated by XHarness')
    exit(1)

if not os.path.isfile(diagnostics_file):
    print(f"WARNING: Diagnostics file not found at `{diagnostics_file}`")
    exit(2)

output_directory = os.getenv('HELIX_WORKITEM_UPLOAD_ROOT')

# For the first operation that causes a retry/reboot, we send a metric to Helix
# Retry/reboot can be also asked for by the client (by creating .retry/.reboot files)
retry = False
reboot = False
retry_dimensions = dict()
reboot_dimensions = dict()
retry_exit_code = -1
reboot_exit_code = -1
android_connectivity_verified = False

class AdditionalTelemetryRequired(Exception):
    """Exception raised when we need to send additional telemetry during analysis

    Attributes:
        metric_name -- name of the event
        metric_value -- value of the metric event
    """

    def __init__(self, metric_name, metric_value):
        self.metric_name = metric_name
        self.metric_value = metric_value
        super().__init__("Additional telemetry required")

def call_xharness(args: list, capture_output: bool = False) -> Tuple[int, str]:
    """ Calls the XHarness CLI with given arguments
    """

    xharness_cli_path = os.getenv('XHARNESS_CLI_PATH')
    args = ['dotnet', 'exec', xharness_cli_path] + args

    if capture_output:
        process = subprocess.Popen(args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        stdout = process.communicate()[0].decode("utf-8")
        return process.returncode, stdout
    else:
        return subprocess.run(args, stdout=None, stderr=None, text=True).returncode, None

def call_adb(args: list, capture_output: bool = False):
    """ Calls the XHarness CLI with `android adb` command and given arguments
    """

    return call_xharness(['android', 'adb', '--'] + args, capture_output)

def remove_android_apps(device: str = None):
    """ Removes all Android applications from the target device/emulator
    """

    print('    Removing installed apps after unsuccessful run' + (' from ' + device if device else ""))

    # Get list of installed apps
    args = ['shell', 'pm', 'list', 'packages', 'net.dot']
    if device:
        args = ['-s', device] + args

    exit_code, output = call_adb(args, capture_output=True)
    if exit_code != 0:
        print(f'    Failed to get list of installed apps: {output}')
        return

    installed_apps = output.splitlines()
    installed_apps = [app.split(':')[1] for app in installed_apps if app and app.startswith('package:')]

    # Remove all installed apps
    for app in installed_apps:
        print(f'        Removing {app}')

        args = ['uninstall', app]
        if device:
            args = ['-s', device] + args

        exit_code, _ = call_adb(args)

        if exit_code != 0:
            print(f'            Failed to remove app {app}')

def analyze_operation(command: str, platform: str, device: str, is_device: bool, target: str, exit_code: int):
    """ Analyzes the result and requests retry/reboot in case of an infra failure
        Too see where the exit code values come from, see https://github.com/dotnet/xharness/blob/master/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs
    """

    print(f'Analyzing {platform}/{command}@{target} ({exit_code})')

    global retry, reboot, android_connectivity_verified

    # Kill the simulator when we fail to launch the app
    if exit_code == 80 and not is_device: # APP_CRASH
        print(f'    Application crashed - if persist, please investigate system logs from the run')
        retry = True
        reboot = True
        return

    # Simulators are known to slow down which results in installation taking several minutes
    # Retry+reboot usually resolves this
    if exit_code == 86: # APP_INSTALLATION_TIMEOUT
        print(f'    Installation timed out')
        reboot = True
        retry = True
        return

    # Simulators are known to slow/break down and a reboot usually helps
    # This manifest by us not being able to launch the simulator
    if exit_code == 88: # SIMULATOR_FAILURE
        print(f'    Failed to launch the emulator')
        reboot = True
        retry = True
        return

    # Devices can be locked or in a corrupted state, in this case we only retry the work item
    if exit_code == 89: # DEVICE_FAILURE
        print(f'    Failed to talk to the device')
        retry = True
        return

    if exit_code == 90: # APP_LAUNCH_TIMEOUT
        print(f'    Failed to launch the app in alloted time')
        if not is_device:
            reboot = True
        retry = True
        return

    if platform == "android":
        if exit_code == 81: # DEVICE_NOT_FOUND
            # This handles issues where emulators fail to start or devices go silent.
            print(f'    Encountered DEVICE_NOT_FOUND')
            print('    If this occurs repeatedly, please check for architectural mismatch, e.g. sending arm64_v8a APKs to an x86_64 / x86 only queue')
            retry = True

            if not is_device:
                # For emulators it makes sense to reboot to try to heal the emulator
                reboot = True

                # We also attach logs from the emulator boot (which might tell us why there's no emulator)
                if os.name != 'nt':
                    # This is where Azure stores logs from custom extension script runs
                    # More details here https://docs.microsoft.com/en-us/azure/virtual-machines/extensions/custom-script-linux#troubleshooting
                    boot_log_location = '/var/lib/waagent/custom-script/download'

                    print(f'    Collecting emulator boot logs from {boot_log_location}..')
                    boot_log_destination = output_directory + '/emulator_logs'

                    # Only copy stdout/stderr files (however they might be in different folders based on how Azure executed extension scripts)
                    subprocess.call(['sudo', 'rsync', '--recursive', '--include', 'stdout', '--include', 'stderr', '--filter', '-! */',
                        boot_log_location, boot_log_destination])

                    # The boot logs are owned by root, so make them readable for the Helix agent
                    subprocess.call(['sudo', 'chmod', '-R', '777', boot_log_destination])

            return

        if exit_code == 82 and not is_device: # RETURN_CODE_NOT_SET - this happens when emulator crashes halfway through
            print(f'    Failed to read the instrumentation result')
            retry = True
            reboot = True
            return

        if exit_code == 78: # PACKAGE_INSTALLATION_FAILURE
            # This handles issues where APKs fail to install.
            # We already reboot a device inside XHarness and now request a work item retry when this happens
            print(f'    Encountered PACKAGE_INSTALLATION_FAILURE')
            print('    If this occurs repeatedly, please check for architectural mismatch, e.g. requesting installation on arm64_v8a-only queue for x86 or x86_64 APKs')
            retry = True

            if is_device:
                try:
                    remove_android_apps(device)
                except Exception as e:
                    print(f'    Failed to remove installed apps from device: {e}')

            return

        if exit_code == 91: # ADB_FAILURE
            # This handles issues where we have problems with ADB
            # The only solution is to reboot the machine, so we request a work item retry + agent reboot when this happens
            print(f'    Encountered ADB_FAILURE')
            print('    If this occurs repeatedly, please check for architectural mismatch, e.g. sending arm64_v8a APKs to an x86_64 / x86 only queue')

            if not is_device and os.name != 'nt':
                # Copy emulator log
                subprocess.call(['cp', '/tmp/*-logcat.log', output_directory])

            reboot = True
            retry = True
            return

        if exit_code != 0 and is_device and not android_connectivity_verified:
            # Any issue can also be caused by network connectivity problems (devices sometimes lose the WiFi connection)
            # In those cases, we want a retry and we want to report this
            print('    Encountered non-zero exit code. Checking network connectivity...')
            android_connectivity_verified = True

            exitcode, _ = call_adb(['shell', 'ping', '-i', '0.2', '-c', '3', 'www.microsoft.com'])

            if exitcode != 0:
                retry = True
                print(f'    Detected network connectivity issue')
                raise AdditionalTelemetryRequired(NETWORK_CONNECTIVITY_METRIC_NAME, 1)

    elif platform == "apple":
        # This code should only be retried for Apple as in Android this can mean failed tests or app crash
        if exit_code == 82: # RETURN_CODE_NOT_SET
            # See https://github.com/dotnet/xharness/issues/812
            print(f'    Failed to detect app\'s exit code')
            retry = True
            return

        # If we have a launch failure on simulators, we want a reboot+retry
        # We want retry only on devices (it happens quite rarely)
        if exit_code == 83: # APP_LAUNCH_FAILURE
            print(f'    Encountered APP_LAUNCH_FAILURE')
            if not is_device:
                reboot = True
            retry = True
            return

        if is_device:
            # If we fail to find a real device, it is unexpected as device queues should have one
            if exit_code == 81: # DEVICE_NOT_FOUND
                print(f'    Requested tethered Apple device not found')
                reboot = True
                retry = True
                return

        else:
            if exit_code == 78: # PACKAGE_INSTALLATION_FAILURE
                print(f'    Encountered PACKAGE_INSTALLATION_FAILURE. This might be caused by a corrupt simulator')
                retry = True
                reboot = True
                return

            # If we fail to find a simulator and we are not targeting a specific version (e.g. `ios-simulator_13.5`),
            # it is probably an issue because Xcode should always have at least one runtime version inside
            if exit_code == 81 and '_' not in target: # DEVICE_NOT_FOUND
                print(f'    No simulator runtime found')
                retry = True
                reboot = True
                return

# The JSON should be an array of objects (one per each executed XHarness command)
try:
    operations = json.load(open(diagnostics_file))
except Exception as e:
    print(f'    Failed to load the diagnostics file: {e}')
    print('Diagnostics file contents:')
    with open(diagnostics_file) as f:
        print(f.read())
    exit(1)

if len(operations) == 0:
    print('    No operations found in the diagnostics file')
    exit(0)

print(f"Reporting {len(operations)} events from diagnostics file `{diagnostics_file}`")

# Example version: 1.0.0-prerelease.22269.1+6e87004b51c89c59ac4a34536e9bc22da0124f39
# We remove the commit SHA
_, version = call_xharness(['version'], capture_output=True)
version = version.strip().split("+")[0]

# Parse operations, analyze them and send them to Application Insights
for operation in operations:
    command = operation['command']
    platform = operation['platform']
    exit_code = operation['exitCode']
    duration = operation['duration']
    device = operation.get('device')
    target = operation.get('target')
    target_os = operation.get('targetOS')
    is_device = operation.get('isDevice', None)

    custom_dimensions = dict()
    custom_dimensions['command'] = command
    custom_dimensions['platform'] = platform
    custom_dimensions['version'] = version

    if is_device is not None:
        custom_dimensions['isDevice'] = 'true' if str(is_device).lower() == 'true' else 'false'

    if 'target' in operation:
        if 'targetOS' in operation:
            custom_dimensions['target'] = target + ':' + target_os
        else:
            custom_dimensions['target'] = target
    elif 'targetOS' in operation:
        custom_dimensions['target'] = target_os

    try:
        analyze_operation(command, platform, device, is_device, target, exit_code)
    except AdditionalTelemetryRequired as e:
        send_metric(e.metric_name, e.metric_value, custom_dimensions, event_type=EVENT_TYPE)
    except Exception as e:
        print(f'    Failed to analyze operation: {e}')

    # Note down the dimensions that caused retry/reboot
    if retry and retry_dimensions is None:
        retry_dimensions = custom_dimensions
        retry_exit_code = exit_code

    if reboot and reboot_dimensions is None:
        reboot_dimensions = custom_dimensions

    kusto_metrics = dict()
    kusto_metrics[OPERATION_METRIC_NAME] = exit_code
    kusto_metrics[DURATION_METRIC_NAME] = duration

    send_metrics(kusto_metrics, custom_dimensions, event_type=EVENT_TYPE)

# Retry / reboot is handled here
script_dir = os.getenv('HELIX_WORKITEM_ROOT')

if os.path.exists(os.path.join(script_dir, '.retry')):
    retry = True

if os.path.exists(os.path.join(script_dir, '.reboot')):
    reboot = True

if retry:
    send_metric(RETRY_METRIC_NAME, retry_exit_code, retry_dimensions, event_type=EVENT_TYPE)
    request_infra_retry('Requesting work item retry because an infrastructure issue was detected on this machine')

    # TODO https://github.com/dotnet/core-eng/issues/15059
    # We need to remove testResults.xml so that it is not uploaded since this run will be discarded
    # This is a workaround until we make AzDO reporter not upload test results
    file_name = "testResults.xml"
    test_results = os.path.join(output_directory, file_name)
    if os.path.exists(test_results):
        os.remove(test_results)

    if os.path.exists(file_name):
        os.remove(file_name)

if reboot:
    send_metric(REBOOT_METRIC_NAME, reboot_exit_code, reboot_dimensions, event_type=EVENT_TYPE)
    request_reboot('Requesting machine reboot as an infrastructure issue was detected on this machine')
