#!/bin/bash

###
### This script is used as a payload of Helix jobs that execute iOS/tvOS workloads through XHarness.
### This is the entrypoint of the job that goes on to spawn the real payload in user session with
### GUI rendering capabilities.
###

app=''
forwarded_args=''

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | tr "[:upper:]" "[:lower:]")"
    case "$opt" in
      --app)
        app="$2"
        shift
        ;;
      *)
        forwarded_args="$forwarded_args $1"
        ;;
    esac
    shift
done

set -x

# It is important we call the script via `launchctl asuser` in order to be able to spawn
# the simulator which needs to run in a user session with GUI rendering capabilities.
chmod +x xharness-runner.apple.sh
helix_runner_uid=$(id -u)
sudo launchctl asuser "$helix_runner_uid" sh ./xharness-runner.apple.sh \
    $forwarded_args                                                     \
    --app "$HELIX_WORKITEM_ROOT/$app"                                   \
    --xharness-cli-path "$XHARNESS_CLI_PATH"                            \
    --output-directory "$HELIX_WORKITEM_UPLOAD_ROOT"                    \
    --diagnostics-path "$XHARNESS_DIAGNOSTICS_PATH"                     \

exit_code=$?

# Collect diagnostics data and upload it
"$HELIX_PYTHONPATH" "$HELIX_WORKITEM_PAYLOAD/xharness-event-reporter.py"

# For some of the issues such as Simulators get reeaally slow and they start failing to install apps,
# the only solution is to reboot the machine.
# We usually also ask the work item to be re-tried on a different machine
# Since we run the payload script using launchctl, env vars such as PYTHON_PATH are not set there and we have to do this part here
# We signal this by creating files
if [ -f './.retry' ]; then
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_infra_retry; request_infra_retry('Retrying because XHarness ended with $exit_code')"
fi

if [ -f './.reboot' ]; then
    "$HELIX_PYTHONPATH" -c "from helix.workitemutil import request_reboot; request_reboot('Rebooting because XHarness ended with $exit_code')"
fi

exit $exit_code
