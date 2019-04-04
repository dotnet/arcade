#!/usr/bin/env bash

usage()
{
  echo "Usage"
}

configuration_specified=0
while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | awk '{print tolower($0)}')"
  shift
  case "$opt" in
    -configuration|-c)
      if [[ "$configuration_specified" == '1' ]] ; then
        usage
        exit 1
      fi
      configuration_specified=1
      if [[ $# == 0 ]]; then
        usage
        exit 1
      fi
      configuration=$1
      ;;
    *)
      usage
      exit 1
      ;;
  esac

  shift
done

if [[ "$configuration_specified" == '0' ]] ; then
  usage
  exit 1
fi

echo "The configuration is '$configuration'"