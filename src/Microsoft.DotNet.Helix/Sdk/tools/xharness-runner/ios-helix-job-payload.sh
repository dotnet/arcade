#!/bin/bash

set -ex

app=''
output_directory=''
targets=''
timeout=''
launch_timeout=''
xharness_cli_path=''
xcode_version=''
app_arguments=''

while [[ $# > 0 ]]; do
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
      *)
        echo "Invalid argument: $1"
        exit 1
        ;;
    esac
    shift
done

function die ()
{
    echo $1 1>&2
    exit 1
}

if [ -z "$app" ]; then
    die "App name wasn't provided";
fi

if [ -z "$output_directory" ]; then
    die "Output directory wasn't provided";
fi

if [ -z "$targets" ]; then
    die "List of targets wasn't provided";
fi

if [ -z "$timeout" ]; then
    die "Test timeout wasn't provided";
fi

if [ -z "$launch_timeout" ]; then
    die "Launch timeout wasn't provided";
fi

if [ -z "$xharness_cli_path" ]; then
    die "XHarness path wasn't provided";
fi

if [ -z "$xcode_version" ]; then
    die "Xcode version wasn't provided";
fi

if [ ! -z "$app_arguments" ]; then
    app_arguments="-- $app_arguments";
fi

set +e

# Restart the simulator to make sure it is tied to the right user session
xcode_path="/Applications/Xcode${xcode_version/./}.app"
simulator_app="$xcode_path/Contents/Developer/Applications/Simulator.app"
sudo pkill -9 -f "$simulator_app"
open -a "$simulator_app"

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

dotnet exec "$xharness_cli_path" ios test  \
    --app="$app"                           \
    --output-directory="$output_directory" \
    --targets="$targets"                   \
    --timeout=$timeout                     \
    --launch-timeout=$launch_timeout       \
    --xcode="$xcode_path"                  \
    -v                                     \
    $app_arguments

exit_code=$?

# Kill the simulator after we're done
sudo pkill -9 -f "$simulator_app"

# The simulator logs comming from the sudo-spawned Simulator.app are not readable by the helix uploader
chmod 0644 "$output_directory"/*.log

test_results=`ls "$output_directory"/xunit-*.xml`

if [ ! -f "$test_results" ]; then
    echo "Failed to find xUnit tests results in the output directory. Existing files:"
    ls -la "$output_directory"
    exit 1
fi

echo "Found test results in $output_directory/$test_results. Renaming to testResults.xml to prepare for Helix upload"

# Prepare test results for Helix to pick up
mv "$test_results" "$output_directory/testResults.xml"

exit $exit_code
