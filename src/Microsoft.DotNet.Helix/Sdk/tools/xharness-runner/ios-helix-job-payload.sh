#!/bin/bash

set -x
set -e

# TODO: Remove before check-in
dotnet --version

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
      --dotnet-root)
        dotnet_root="$2"
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
    echo $2 1>&2
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

if [ -z "$dotnet_root" ]; then
    die "DotNet root path wasn't provided";
fi

export XHARNESS_DISABLE_COLORED_OUTPUT=true
export XHARNESS_LOG_WITH_TIMESTAMPS=true

# Restart the simulator to make sure it is tied to the right user session
xcode_path=`xcode-select -p`
pid=`ps aux | grep $xcode_path/Applications/Simulator.app | grep -v grep | tr -s ' ' | cut -d ' ' -f 2`
if [ ! -z "$pid" ]; then
    sudo kill $pid
fi
open -a $xcode_path/Applications/Simulator.app

"$dotnet_root/dotnet" xharness ios test    \
    --app="$app"                           \
    --output-directory="$output_directory" \
    --targets="$targets"                   \
    --timeout="$timeout"                   \
    -v
