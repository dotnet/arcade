#!/usr/bin/env bash

workspace_path=$1

# Make the workspace path directory.
sudo mkdir $workspace_path

# Make it writeable
sudo chmod -R 755 $workspace_path

# copy .agent and .credentials files to workspace path
cp -f -r $HELIX_CORRELATION_PAYLOAD/* $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.agent $workspace_path
cp -f $HELIX_WORKITEM_PAYLOAD/.credentials $workspace_path

$workspace_path/run.sh

# Expect an exit code of 2, which is what is given when the agent connection is revoked
lastexitcode=$?
if [[ $lastexitcode -ne 2 ]]; then
	echo "Unexpected error returned from agent: $lastexitcode"
	exit $lastexitcode
else
	echo "Agent disconnected successfully, exiting"
	exit 0
fi
