#!/bin/bash

### This script is used as a payload of Helix jobs that execute Android workloads through XHarness on Linux systems.
### This is used as the entrypoint of the work item so that XHarness failures can be detected and (when appropriate)
### cause the work item to retry and reboot the Helix agent the work is running on.
### 
### Currently no special functionality is needed beyond causing infrastructure retry and reboot if the emulators
### or devices have trouble, but to add more Helix-specific Android XHarness behaviors, this is one extensibility point.

set -x
echo "XHarness Helix Job Wrapper calling '$@'"
"$@"
exit_code=$?

# This handles issues where devices or emulators fail to start.
# The only solution is to reboot the machine, so we request a work item retry + agent reboot when this happens
# Too see where these values come from, check out https://github.com/dotnet/xharness/blob/master/src/Microsoft.DotNet.XHarness.Common/CLI/ExitCode.cs
# Avoid any helix-ism in the Xharness!

ADB_DEVICE_ENUMERATION_FAILURE=85 
if [ $exit_code -eq $ADB_DEVICE_ENUMERATION_FAILURE ]; then
    echo 'Encountered ADB_DEVICE_ENUMERATION_FAILURE.  This is typically not a failure of the work item.  We will run it again and reboot this computer to help its devices'
    echo 'If this occurs repeatedly, please check for architectural mismatch, e.g. sending arm64_v8a APKs to an x86_64 / x86 only queue.'
    # Since we run the payload script using launchctl, env vars are not set there and we have to do this part here
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because we could not enumerate all Android devices')"
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting to allow Android emulator or device to restart.')"
fi

exit $exit_code
