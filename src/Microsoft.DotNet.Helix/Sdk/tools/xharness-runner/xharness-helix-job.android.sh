#!/bin/bash

### This script is used as a payload of Helix jobs that execute Android workloads through XHarness on Linux systems.
### This is used as the entrypoint of the work item so that XHarness failures can be detected and (when appropriate)
### cause the work item to retry and reboot the Helix agent the work is running on.
###
### This scripts sets up the environment and then sources the actual XHarness commands that come either from the Helix
### SDK or from user via the <CustomCommands> property.

echo "XHarness Helix Job Wrapper calling '$@'"

set -x

app=''
timeout=''
package_name=''
expected_exit_code=0
device_output_path=''
instrumentation=''
output_directory=$HELIX_WORKITEM_UPLOAD_ROOT

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | tr "[:upper:]" "[:lower:]")"
    case "$opt" in
      --app)
        app="$2"
        shift
        ;;
      --timeout)
        timeout="$2"
        shift
        ;;
      --package_name)
        package_name="$2"
        shift
        ;;
      --expected_exit_code)
        expected_exit_code="$2"
        shift
        ;;
      --device_output_path)
        device_output_path="$2"
        shift
        ;;
      --instrumentation)
        instrumentation="$2"
        shift
        ;;
    esac
    shift
done

if [ -z "$app" ]; then
    die "App bundle path wasn't provided";
fi

if [ -z "$timeout" ]; then
    die "No timeout was provided";
fi

if [ -z "$package_name" ]; then
    die "Package name path wasn't provided";
fi

if [ -z "$output_directory" ]; then
    die "No output directory provided";
fi

# The xharness alias
function xharness() {
    dotnet exec $XHARNESS_CLI_PATH "$@"
}

# Act out the actual commands
source command.sh

exit_code=$?

retry=false
reboot=false

# Too see where these values come from, check out https://github.com/dotnet/xharness/blob/master/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs
# Avoid any helix-ism in the Xharness!

case "$exit_code" in
  85)
    # This handles issues where devices or emulators fail to start.
    # The only solution is to reboot the machine, so we request a work item retry + agent reboot when this happens
    echo 'Encountered ADB_DEVICE_ENUMERATION_FAILURE. This is typically not a failure of the work item. We will run it again and reboot this computer to help its devices'
    echo 'If this occurs repeatedly, please check for architectural mismatch, e.g. sending arm64_v8a APKs to an x86_64 / x86 only queue.'
    retry=true
    reboot=true
    ;;
  78)
    # This handles issues where APKs fail to install.
    # We already reboot a device inside XHarness and now request a work item retry when this happens
    echo 'Encountered PACKAGE_INSTALLATION_FAILURE. This is typically not a failure of the work item. We will try it again on another Helix agent'
    echo 'If this occurs repeatedly, please check for architectural mismatch, e.g. requesting installation on arm64_v8a-only queue for x86 or x86_64 APKs.'
    retry=true
    ;;
esac



ADB_DEVICE_ENUMERATION_FAILURE=85
if [ "$retry"]; then
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
fi

PACKAGE_INSTALLATION_FAILURE=78
if [ $exit_code -eq PACKAGE_INSTALLATION_FAILURE ]; then
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart.')"
fi

exit $exit_code
