#!/bin/bash

###
### This script is used as a payload of Helix jobs that execute iOS/tvOS workloads through XHarness.
### This is the entrypoint of the job that goes on to spawn the real payload in user session with
### GUI rendering capabilities.
###

set -x

chmod +x xharness-runner.apple.sh
helix_runner_uid=$(id -u)
sudo launchctl asuser "$helix_runner_uid" sh ./xharness-runner.apple.sh "$@"
exit_code=$?

# This handles an issue where Simulators get reeaally slow and they start failing to install apps
# The only solution is to reboot the machine, so we request a work item retry + MacOS reboot when this happens
# 123 - timeout in installation on Simulators
installation_timeout_exit_code=123
if [ $exit_code -eq $installation_timeout_exit_code ]; then
    # Since we run the payload script using launchctl, env vars are not set there and we have to do this part here
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because iOS Simulator application install hung')"
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting because iOS Simulator application install hung ')"
fi

exit $exit_code
