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

repoRoot="$scriptroot/../.."
globalJsonFile="$repoRoot/global.json"

installPath="$repoRoot/.dotnet"
version=`cat $globalJsonFile | grep "\"version\"" | head -n 1 | cut -d : -f 2 | xargs echo`

if [ -z $version ]; then
  echo "Version not found in $globalJsonFile"
  exit -1
fi

if command -v dotnet && [ `dotnet --version` = $version ]; then
  echo "Global dotnet cli version $version found"
  exit 0
fi

dotnetinstallscript="$installPath/dotnet-install.sh"

if [ ! -e $dotnetinstallscript ]; then
  mkdir -p $installPath

  # Use curl if available, otherwise use wget
  if command -v curl > /dev/null; then
    curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$dotnetinstallscript"
  else
    wget -q -O "$dotnetinstallscript" "https://dot.net/v1/dotnet-install.sh"
  fi
fi

bash $dotnetinstallscript -Version $version -InstallDir $installPath
installCode=$?
if [ ! $installCode ]; then
  echo "Failed to install dotnet cli (exit code $installCode)"
  exit $installCode
fi

if [ ! -z $BUILD_BUILDNUMBER ]; then
  echo "VSTS detected, setting up Dotnet Cli for use"
  echo "##vso[task.prependpath]$installPath"
  echo "##vso[task.setvariable variable=DOTNET_SKIP_FIRST_TIME_EXPERIENCE;]1"
  echo "##vso[task.setvariable variable=DOTNET_MULTILEVEL_LOOKUP;]0"
fi
export PATH="$installPath:"$PATH""
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0
echo "Installed Dotnet Cli"
