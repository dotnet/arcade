#!/bin/bash

###
### This script is used as a payload of Helix jobs that execute iOS/tvOS workloads through XHarness.
### It is executed via `launchctl asuser` in order to be able to spawn the simulator which needs to
### run in a user session with GUI rendering capabilities.
###

set -ex

app=''
output_directory=''
targets=''
timeout=''
launch_timeout=''
xharness_cli_path=''
xcode_version=''
app_arguments=''
expected_exit_code=0
command='test'

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | awk '{print tolower($0)}')"
    case "$opt" in
      --app)
        app="$2"
        shift
        ;;
      --output-directory)
        output_directory="$2"
        shift
        ;;
      --targets)
        targets="$2"
        shift
        ;;
      --timeout)
        timeout="$2"
        shift
        ;;
      --launch-timeout)
        launch_timeout="$2"
        shift
        ;;
      --xharness-cli-path)
        xharness_cli_path="$2"
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
      --command)
        command="$2"
        shift
        ;;
      *)
        echo "Invalid argument: $1"
        exit 1
        ;;
    esac
    shift
done

function die ()
{
    echo "$1" 1>&2
    exit 1
}

if [ -z "$timeout" ]; then
    die "Test timeout wasn't provided";
fi

if [ -z "$xharness_cli_path" ]; then
    die "XHarness path wasn't provided";
fi

if [ -n "$app_arguments" ]; then
    app_arguments="-- $app_arguments";
fi

if [ "$command" == "run" ]; then
    app_arguments="--expected-exit-code=$expected_exit_code $app_arguments"
elif [ -n "$launch_timeout" ]; then
    # shellcheck disable=SC2089
    app_arguments="--launch-timeout=$launch_timeout $app_arguments"
fi

set +e

if [ -z "$xcode_version" ]; then
    xcode_path="$(dirname "$(dirname "$(xcode-select -p)")")"
else
    xcode_path="/Applications/Xcode${xcode_version/./}.app"
fi

# Start the simulator if it is not running already
simulator_app="$xcode_path/Contents/Developer/Applications/Simulator.app"
open -a "$simulator_app"

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

# We include $app_arguments non-escaped and not arrayed because it might contain several extra arguments
# which come from outside and are appeneded behind "--" and forwarded to the iOS application from XHarness.
# shellcheck disable=SC2086,SC2090
dotnet exec "$xharness_cli_path" ios $command \
    --app="$app"                              \
    --output-directory="$output_directory"    \
    --targets="$targets"                      \
    --timeout="$timeout"                      \
    --xcode="$xcode_path"                     \
    -v                                        \
    $app_arguments

exit_code=$?

# Kill the simulator just in case when we fail to launch the app
# 80 - app crash
if [ $exit_code -eq 80 ]; then
    sudo pkill -9 -f "$simulator_app"
fi

# The simulator logs comming from the sudo-spawned Simulator.app are not readable by the helix uploader
chmod 0644 "$output_directory"/*.log

if [ "$command" == 'test' ]; then
    test_results=$(ls "$output_directory"/xunit-*.xml)

    if [ ! -f "$test_results" ]; then
        echo "Failed to find xUnit tests results in the output directory. Existing files:"
        ls -la "$output_directory"

        if [ $exit_code -eq 0 ]; then
            exit_code=5
        fi

        exit $exit_code
    fi

    echo "Found test results in $output_directory/$test_results. Renaming to testResults.xml to prepare for Helix upload"

    # Prepare test results for Helix to pick up
    mv "$test_results" "$output_directory/testResults.xml"
fi

exit $exit_code
