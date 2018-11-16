#!/bin/sh
set -x

script_path=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

ENV_PATH=$HOME/.vsts-env
date -u +"%FT%TZ"

if [ ! -f $ENV_PATH/bin/python ]; then
  $HELIX_PYTHONPATH -m virtualenv --no-site-packages $ENV_PATH
  $ENV_PATH/bin/python -m pip install vsts==0.1.20
fi

date -u +"%FT%TZ"
$ENV_PATH/bin/python $script_path/run.py "$@"
date -u +"%FT%TZ"
