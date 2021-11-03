import getopt
import json
import os
import subprocess
import sys

from helix.appinsights import app_insights
from helix.workitemutil import request_reboot, request_infra_retry

### This script's purpose is to parse the diagnostics.json file produced by XHarness, evaluate it and send it to AppInsights
### The diagnostics.json file contains information about each XHarness command executed during the job
### In case of events that suggest infrastructure issues, we request a retry and for some reboot the agent

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
retry = False
reboot = False

def remove_android_apps(device: str = None):
    """ Removes all Android applications from the target device/emulator
    """

    print(f'    Removing installed apps after unsuccessful run' + (' from ' + device if device else ""))

    xharness_cli_path = os.getenv('XHARNESS_CLI_PATH')
    adb_path = subprocess.check_output(['dotnet', 'exec', xharness_cli_path, 'android', 'state', '--adb']).decode('utf-8').strip()

    # Get list of installed apps
    if device:
        installed_apps = subprocess.check_output([adb_path, '-s', device, 'shell', 'pm', 'list', 'packages', 'net.dot']).decode('utf-8').splitlines()
    else:
        installed_apps = subprocess.check_output([adb_path, 'shell', 'pm', 'list', 'packages', 'net.dot']).decode('utf-8').splitlines()

    installed_apps = [app.split(':')[1] for app in installed_apps if app]

    # Remove all installed apps
    for app in installed_apps:
        print(f'        Removing {app}')

        if device:
            result = subprocess.run([adb_path, '-s', device, 'uninstall', app], stdout=subprocess.PIPE)
        else:
            result = subprocess.run([adb_path, 'uninstall', app], stderr=subprocess.STDOUT)

        output = result.stdout.decode('utf8')
        print(f'            {output}')

def analyze_operation(command: str, platform: str, device: str, isDevice: bool, target: str, exitCode: int):
    """ Analyzes the result and requests retry/reboot in case of an infra failure
        Too see where the exit code values come from, see https://github.com/dotnet/xharness/blob/master/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs
    """

    print(f'Analyzing {platform}/{command}@{target} ({exitCode})')

    global retry, reboot

    if platform == "android":
        if exitCode == 85: # ADB_DEVICE_ENUMERATION_FAILURE
            # This handles issues where devices or emulators fail to start.
            # The only solution is to reboot the machine, so we request a work item retry + agent reboot when this happens
            print('    Encountered ADB_DEVICE_ENUMERATION_FAILURE. This is typically not a failure of the work item. It will be run again. This machine will reboot to help its devices')
            print('    If this occurs repeatedly, please check for architectural mismatch, e.g. sending arm64_v8a APKs to an x86_64 / x86 only queue.')

            if not isDevice and os.name != 'nt':
                # Copy emulator log
                subprocess.call(['cp', '/tmp/*-logcat.log', output_directory])

            reboot = True
            retry = True

        if exitCode == 78: # PACKAGE_INSTALLATION_FAILURE
            # This handles issues where APKs fail to install.
            # We already reboot a device inside XHarness and now request a work item retry when this happens
            print('    Encountered PACKAGE_INSTALLATION_FAILURE. This is typically not a failure of the work item. We will try it again on another Helix agent')
            print('    If this occurs repeatedly, please check for architectural mismatch, e.g. requesting installation on arm64_v8a-only queue for x86 or x86_64 APKs.')

            if isDevice:
                try:
                    remove_android_apps(device)
                except Exception as e:
                    print(f'    Failed to remove installed apps from device: {e}')

            retry = True

    elif platform == "apple":
        retry_message = 'This is typically not a failure of the work item. It will be run again. '
        reboot_message = 'This machine will reboot to heal.'

        if isDevice:
            # If we fail to find a real device, it is unexpected as device queues should have one
            # It can often be fixed with a reboot
            if exitCode == 81: # DEVICE_NOT_FOUND
                print(f'    Requested tethered Apple device not found. {retry_message}{reboot_message}')
                reboot = True
                retry = True

            # Devices can be locked or in a corrupted state, in this case we only retry the work item
            if exitCode == 89: # DEVICE_FAILURE
                print(f'    Failed to launch the simulator. {retry_message}')
                retry = True
        else:
            # Kill the simulator when we fail to launch the app
            if exitCode == 80: # APP_CRASH
                simulator_app = os.getenv('SIMULATOR_APP')
                subprocess.call(['sudo', 'pkill', '-9', '-f', simulator_app])

            # If we have a launch failure on simulators, we want a reboot+retry
            if exitCode == 83: # APP_LAUNCH_FAILURE
                print(f'    Encountered APP_LAUNCH_FAILURE. {retry_message}{reboot_message}')
                reboot = True
                retry = True

            # If we fail to find a simulator and we are not targeting a specific version (e.g. `ios-simulator_13.5`),
            # it is probably an issue because Xcode should always have at least one runtime version inside
            if exitCode == 81 and '_' not in target: # DEVICE_NOT_FOUND
                print(f'    No simulator runtime found. {retry_message}')
                retry = True

            # Simulators are known to slow down which results in installation taking several minutes
            # Retry+reboot usually resolves this
            if exitCode == 86: # APP_INSTALLATION_TIMEOUT
                print(f'    Installation timed out. {retry_message}{reboot_message}')
                reboot = True
                retry = True

            # Simulators are known to slow/break down and a reboot usually helps
            # This manifest by us not being able to launch the simulator
            if exitCode == 88: # SIMULATOR_FAILURE
                print(f'    Failed to launch the simulator. {retry_message}{reboot_message}')
                reboot = True
                retry = True

# The JSON should be an array of objects (one per each executed XHarness command)
operations = json.load(open(diagnostics_file))

print(f"Reporting {len(operations)} events from diagnostics file `{diagnostics_file}`")

# Parse operations, analyze them and send them to Application Insights

for operation in operations:
    command = operation['command']
    platform = operation['platform']
    exitCode = operation['exitCode']
    duration = operation['duration']
    device = operation.get('device')
    target = operation.get('target')
    targetOS = operation.get('targetOS')
    isDevice = operation.get('isDevice', False)

    try:
        analyze_operation(command, platform, device, isDevice, target, exitCode)
    except Exception as e:
        print(f'    Failed to analyze operation: {e}')

    custom_dimensions = dict()
    custom_dimensions['command'] = operation['command']
    custom_dimensions['platform'] = operation['platform']

    if 'target' in operation:
        if 'targetOS' in operation:
            custom_dimensions['target'] = target + '_' + targetOS
        else:
            custom_dimensions['target'] = target
    elif 'targetOS' in operation:
        custom_dimensions['target'] = targetOS

    # TODO app_insights.send_metric('XHarnessOperation', exitCode, properties=custom_dimensions)
    # TODO app_insights.send_metric('XHarnessOperationDuration', duration, properties=custom_dimensions)

# Retry / reboot is handled here

if os.path.exists('./.retry'):
    retry = True

if os.path.exists('./.reboot'):
    reboot = True

if retry:
    # TODO request_infra_retry('Requesting work item retry because an infrastructure issue was detected on this machine')
    print('Requesting work item retry because an infrastructure issue was detected on this machine')

if reboot:
    # TODO request_reboot('Requesting machine reboot as an infrastructure issue was detected on this machine')
    print('Requesting machine reboot as an infrastructure issue was detected on this machine')
