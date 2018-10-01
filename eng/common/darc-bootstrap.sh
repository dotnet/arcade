#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  [[ $source != /* ]] && source="$scriptroot/$source"
done

scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
darc_cli_package_name="microsoft.dotnet.darc"

function ReadJson {
  local key=$1
  local file=$2

  local unamestr="$(uname)"
  local sedextended='-r'
  if [[ "$unamestr" == 'Darwin' ]]; then
    sedextended='-E'
  fi;

  local match="$(grep -m 1 "\"$key\"" $file | sed $sedextended 's/^ *//;s/.*: *"//;s/",?//')"
  if [[ ! "$match" ]]; then
    echo "Error: Cannot find \"$key\" in $file" >&2;
    exit 1
  fi;

  # return value
  echo "$match"
}

function InstallDotnetCli {
  local repo_root="$scriptroot/../.."
  local dotnet_sdk_version=`ReadJson "dotnet" "$repo_root/global.json"`
  local dotnet_root="$repo_root/.dotnet"
  local install_script="$dotnet_root/dotnet-install.sh"
  
  if [[ ! -a "$install_script" ]]; then
    mkdir -p "$dotnet_root"

    if command -v curl > /dev/null; then
      curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$install_script"
    else
      wget -q -O "$install_script" "https://dot.net/v1/dotnet-install.sh"
    fi
  fi
  
  bash "$install_script" --version $dotnet_sdk_version --install-dir $dotnet_root
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to install dotnet SDK (exit code '$lastexitcode')."
    exit $lastexitcode
  fi
}

function InstallDarcCli {
  local darc_cli_feed=`ReadJson "feed" "darc-cli-settings.json"`
  local darc_cli_version=`ReadJson "version" "darc-cli-settings.json"`
  local uninstall_command=`dotnet tool uninstall $darc_cli_package_name -g`
  local tool_list=$(dotnet tool list -g)
  if [[ $tool_list = *$darc_cli_package_name* ]]; then
    echo $(dotnet tool uninstall $darc_cli_package_name -g)
  fi

  echo "Installing Darc CLI version $darc_cli_version..."
  echo $(dotnet tool install $darc_cli_package_name --version $darc_cli_version --add-source $darc_cli_feed -g)
}

InstallDotnetCli
InstallDarcCli
exit $?
