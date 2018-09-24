#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

projects=''
verbosity='minimal'
warnaserrorflag=''
nodereuseflag='/nr:true'
properties=''


while (($# > 0)); do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case $lowerI in
    --verbosity)
      verbosity=$2
      shift 2
      ;;
    --warnaserror)
      if [[ $2 != 0 ]]; then
        warnaserrorflag="/warnaserror"
      fi
      shift 2
      ;;
    --nodereuse)
      if [[ $2 == 0 ]]; then
        nodereuseflag="/nr:false"
      fi
      shift 2
      ;;
    --ci)
      ci=true
      shift 1
      ;;
    --preparemachine)
      prepare_machine=true
      shift 1
      ;;
      *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

. $scriptroot/init-tools.sh

"$build_driver" msbuild \
  /m /nologo /clp:Summary \
  /v:$verbosity \
  /bl:$build_log \
  $warnaserrorflag \
  $nodereuseflag \
  $properties

lastexitcode=$?

if [[ $lastexitcode != 0 ]]; then
  echo "Build failed see log: $build_log"
fi

ExitWithExitCode $lastexitcode