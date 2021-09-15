#!/bin/bash

###
### This script is used as a payload of Helix jobs that execute iOS/tvOS workloads through XHarness.
### This is the entrypoint of the job that goes on to spawn the real payload in user session with
### GUI rendering capabilities.
###

app=''
build_app=false
forwarded_args=''

while [[ $# -gt 0 ]]; do
    opt="$(echo "$1" | tr "[:upper:]" "[:lower:]")"
    case "$opt" in
      --app)
        app="$2"
        shift
        ;;
      --build)
        build_app=true
        ;;
      *)
        forwarded_args="$forwarded_args $1"
        ;;
    esac
    shift
done

set -x

if [ "$build_app" = true ] ; then
    chmod +x build-apple-app.sh
    sh ./build-apple-app.sh
fi

# It is important we call the script via `launchctl asuser` in order to be able to spawn
# the simulator which needs to run in a user session with GUI rendering capabilities.
# The problem with launchctl is that the spawned process won't share environment variables
# so we have to set them again.
export -p > envvars
chmod +x xharness-runner.apple.sh
helix_runner_uid=$(id -u)
sudo launchctl asuser "$helix_runner_uid" sh ./xharness-runner.apple.sh \
    $forwarded_args                                                     \
    --app "$HELIX_WORKITEM_ROOT/$app"                                   \
    --output-directory "$HELIX_WORKITEM_UPLOAD_ROOT"                    \

exit_code=$?

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
