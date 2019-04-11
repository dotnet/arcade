#!/usr/bin/env bash
usage()
{
  echo "Usage"
}
variable_a_specified=0
variable_b_specified=0
while [[ $# > 0 ]]; do
  opt="$(echo "${1/#--/-}" | awk '{print tolower($0)}')"
  shift
  case "$opt" in
    -parameter-a|-a)
      if [[ "$variable_a_specified" == '1' ]] ; then
        usage
        exit 1
      fi
      variable_a_specified=1
      if [[ $# == 0 ]]; then
        usage
        exit 1
      fi
      variable_a=$1
      ;;
    -parameter-b|-b)
      if [[ "$variable_b_specified" == '1' ]] ; then
        usage
        exit 1
      fi
      variable_b_specified=1
      if [[ $# == 0 ]]; then
        usage
        exit 1
      fi
      variable_b=$1
      ;;
    *)
      usage
      exit 1
      ;;
  esac
  shift
done
if [[ "$variable_a_specified" == '0' ]] ; then
  usage
  exit 1
fi
if [[ "$variable_a_specified" == '1' ]] ; then
  echo "The variable_a is '$variable_a'"
fi
if [[ "$variable_b_specified" == '1' ]] ; then
  echo "The variable_b is '$variable_b'"
fi