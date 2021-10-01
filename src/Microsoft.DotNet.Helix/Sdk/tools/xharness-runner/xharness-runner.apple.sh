#!/bin/bash

###
### This script is used as a payload of Helix jobs that execute iOS/tvOS workloads through XHarness.
### It is executed via `launchctl asuser` in order to be able to spawn the simulator which needs to
### run in a user session with GUI rendering capabilities.
###

app=''
target=''
timeout=''
launch_timeout=''
command_timeout=20
xcode_version=''
app_arguments=''
expected_exit_code=0
includes_test_runner=false
reset_simulator=false

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | tr "[:upper:]" "[:lower:]")"
    case "$opt" in
      --app)
        app="$2"
        shift
        ;;
      --target)
        target="$2"
        shift
        ;;
      --timeout)
        timeout="$2"
        shift
        ;;
      --command-timeout)
        command_timeout="$2"
        shift
        ;;
      --launch-timeout)
        launch_timeout="$2"
        shift
        ;;
      --xcode-version)
        xcode_version="$2"
        shift
        ;;
      --app-arguments)
        app_arguments="$2"
        shift
        ;;
      --expected-exit-code)
        expected_exit_code="$2"
        shift
        ;;
      --includes-test-runner)
        includes_test_runner=true
        ;;
      --reset-simulator)
        reset_simulator=true
        ;;
    esac
    shift
done

function die ()
{
    echo "$1" 1>&2
    exit 1
}

function sign ()
{
    echo "Signing $1"

    provisioning_profile="$1/embedded.mobileprovision"
    if [ ! -f "$provisioning_profile" ]; then
        echo "No embedded provisioning profile found at $provisioning_profile! Failed to sign the app!"
        exit 21
    fi

    # Unlock the keychain with certs
    keychain_name='signing-certs.keychain-db'
    keychain_password=$(cat ~/.config/keychain)

    security list-keychains | grep "$keychain_name"
    result=$?
    if [ $result != 0 ]; then
        echo "Keychain '$keychain_name' was not found"
        exit 22
    fi

    security find-identity -vp codesigning "$keychain_name" | grep " 0 valid identities found"
    result=$?
    if [ $result == 0 ]; then
        echo "No valid signing identities found in the keychain"
        exit 23
    fi

    security unlock-keychain -p "$keychain_password" "$keychain_name"

    # Generate entitlements file
    security cms -D -i "$provisioning_profile" > provision.plist
    /usr/libexec/PlistBuddy -x -c 'Print :Entitlements' provision.plist > entitlements.plist

    # Sign the app
    /usr/bin/codesign -v --force --sign "Apple Development" --keychain "$keychain_name" --entitlements entitlements.plist "$1"
}

if [ -z "$app" ]; then
    die "App bundle path wasn't provided";
fi

if [ -z "$target" ]; then
    die "No target was provided";
fi

if [ -z "$xcode_version" ]; then
    xcode_path="$(dirname "$(dirname "$(xcode-select -p)")")"
else
    xcode_path="/Applications/Xcode${xcode_version/./}.app"
fi

# First we need to revive env variables since they were erased by launchctl
# This file already has the expressions in the `export name=value` format
. ./envvars

output_directory=$HELIX_WORKITEM_UPLOAD_ROOT

# Signing
if [ "$target" == 'ios-device' ] || [ "$target" == 'tvos-device' ]; then
    if [ -d "$app" ]; then
        sign "$app"
    else
        echo 'Device target detected but app not found, skipping signing..'
    fi
elif [[ "$target" =~ "simulator" ]]; then
    # Start the simulator if it is not running already
    simulator_app="$xcode_path/Contents/Developer/Applications/Simulator.app"
    open -a "$simulator_app"
fi

# The xharness alias
function xharness() {
    dotnet exec "$XHARNESS_CLI_PATH" "$@"
}

function report_infrastructure_failure() {
    echo "Infrastructural problem reported by the user, requesting retry+reboot: $1"

    touch "$HELIX_WORKITEM_ROOT/.retry"
    touch "$HELIX_WORKITEM_ROOT/.reboot"
}

# Act out the actual commands (and time constrain them to create buffer for the end of this script)
source command.sh & PID=$! ; (sleep $command_timeout && kill $PID 2> /dev/null & ) ; wait $PID
exit_code=$?

# Exit code values - https://github.com/dotnet/xharness/blob/main/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs

# Kill the simulator just in case when we fail to launch the app
# 80 - app crash
if [ $exit_code -eq 80 ] && [[ "$target" =~ "simulator" ]]; then
    sudo pkill -9 -f "$simulator_app"
fi

# If we have a launch failure AND we are on simulators, we need to signal that we want a reboot+retry
# The script that is running this one will notice and request Helix to do it
# 83 - app launch failure
if [ $exit_code -eq 83 ] && [[ "$target" =~ "simulator" ]]; then
    report_infrastructure_failure "Failed to launch the application on a simulator"
fi

# If we fail to find a simulator and we are not targeting a specific version (e.g. `ios-simulator_13.5`),
# it is probably an issue because Xcode should always have at least one runtime version inside
# 81 - simulator/device not found
if [ $exit_code -eq 81 ] && [[ "$target" =~ "simulator" ]] && [[ ! "$target" =~ "_" ]]; then
    report_infrastructure_failure "No simulator runtime found"
fi

# If we fail to find a real device, it is unexpected as device queues should have one
# It can often be fixed with a reboot
# 81 - device not found
if [ $exit_code -eq 81 ] && [[ "$target" =~ "device" ]]; then
    report_infrastructure_failure "Requested tethered Apple device not found"
fi

# Simulators are known to slow down which results in installation taking several minutes
# Retry+reboot usually resolves this
# 86 - app installation timeout
if [ $exit_code -eq 86 ]; then
    report_infrastructure_failure "Installation timed out"
fi

# The simulator logs comming from the sudo-spawned Simulator.app are not readable/deletable by the helix uploader
chmod -R 0766 "$output_directory"

# Remove empty files
find "$output_directory" -name "*.log" -maxdepth 1 -size 0 -print -delete

# Rename test result XML so that AzDO reporter recognizes it
test_results=$(ls "$output_directory"/xunit-*.xml)
if [ -f "$test_results" ]; then
    echo "Found test results in $test_results. Renaming to testResults.xml to prepare for Helix upload"

    # Prepare test results for Helix to pick up
    mv "$test_results" "$output_directory/testResults.xml"
fi

exit $exit_code
