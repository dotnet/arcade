#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

manifest_dir=$1

if [ ! -d "$manifest_dir" ] ; then
  mkdir -p "$manifest_dir"
  echo "Sbom directory created." $manifest_dir
fi

artifact_name=$SYSTEM_STAGENAME$AGENT_JOBNAME"_SBOM"
echo "Artifact name before : "$artifact_name
#replace all special characters with _, some builds use special characters like : in Agent.Jobname, that is not a permissible name while uploading artifacts.
result="${artifact_name//["/:<>\\|?@*$" ]/_}"
echo "Artifact name after : "$result
export ARTIFACT_NAME=$result
echo "##vso[task.setvariable variable=ARTIFACT_NAME]$result"

exit 0
