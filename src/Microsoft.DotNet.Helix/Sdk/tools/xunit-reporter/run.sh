#!/bin/sh
set -x

# relaunch as root
[ `whoami` = root ] || exec sudo -E "$0" "$@"

script_path=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

ENV_PATH=$HOME/.xunit-reporter-env
TMP_ENV_PATH=$HOME/.xunit-reporter-env-tmp
date -u +"%FT%TZ"

if [ ! -f $ENV_PATH/bin/python ]; then
  rm -rf $ENV_PATH
  rm -rf $TMP_ENV_PATH
  $HELIX_PYTHONPATH -m virtualenv --no-site-packages $TMP_ENV_PATH
  mv $TMP_ENV_PATH $ENV_PATH
fi

# Removing pythonpath forces a clean installation of pip packages, but subsequent commands may use HELIX libraries
_OLD_PYTHONPATH=$PYTHONPATH
export PYTHONPATH=

date -u +"%FT%TZ"
$ENV_PATH/bin/python -B $script_path/xunit-reporter.py "$@"
date -u +"%FT%TZ"

export PYTHONPATH=$_OLD_PYTHONPATH